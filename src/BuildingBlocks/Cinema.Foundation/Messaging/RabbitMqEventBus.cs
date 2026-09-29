using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Cinema.Foundation.Messaging;

public class RabbitMqEventBus : IEventBus, IAsyncDisposable
{
    public const string ExchangeName = "cinema.events";

    private readonly string _hostName;
    private readonly string _userName;
    private readonly string _password;
    private readonly int _port;
    private readonly ILogger? _logger;

    private IConnection? _connection;
    private IChannel? _publishChannel;
    private readonly ConcurrentBag<IChannel> _consumerChannels = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    private readonly string _clientProvidedName;

    public RabbitMqEventBus(string? hostName = null, string? userName = null, string? password = null, int port = 5672, string? clientProvidedName = null, ILogger? logger = null)
    {
        _hostName = string.IsNullOrWhiteSpace(hostName) ? "localhost" : hostName;
        _userName = string.IsNullOrWhiteSpace(userName) ? "cinema_app" : userName;
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("RabbitMQ password is required.", nameof(password));
        }
        _password = password;
        _port = port > 0 ? port : 5672;
        _clientProvidedName = string.IsNullOrWhiteSpace(clientProvidedName) 
            ? (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "cinema-service") 
            : clientProvidedName;
        _logger = logger;
    }

    private async Task EnsureConnectedAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            var factory = new ConnectionFactory
            {
                HostName = _hostName,
                UserName = _userName,
                Password = _password,
                Port = _port,
                ClientProvidedName = _clientProvidedName,
                RequestedHeartbeat = TimeSpan.FromSeconds(15)
            };

            const int maxRetries = 10;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _connection = await factory.CreateConnectionAsync();
                    _publishChannel = await _connection.CreateChannelAsync();
                    await _publishChannel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true);
                    _initialized = true;
                    _logger?.LogInformation("Connected to RabbitMQ at {Host}:{Port}, exchange {Exchange}", _hostName, _port, ExchangeName);
                    return;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger?.LogWarning("RabbitMQ connection attempt {Attempt}/{MaxRetries} failed: {Message}. Retrying in 2 seconds...", attempt, maxRetries, ex.Message);
                    await Task.Delay(2000);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "RabbitMQ unavailable at {Host}:{Port} after {MaxRetries} attempts. Cannot start event bus.", _hostName, _port, maxRetries);
                    throw new InvalidOperationException($"RabbitMQ unavailable at {_hostName}:{_port}", ex);
                }
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task PublishAsync<T>(string routingKey, T @event) where T : class
    {
        await EnsureConnectedAsync();

        if (_initialized && _publishChannel != null)
        {
            var json = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(json);

            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await _publishChannel.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: props,
                body: body);

            _logger?.LogInformation("Published event {Type} with key {RoutingKey}", typeof(T).Name, routingKey);
            return;
        }

        throw new InvalidOperationException("RabbitMQ connection is not established. Cannot publish event.");
    }

    public async Task SubscribeAsync<T>(string queueName, string routingKey, Func<T, Task> handler) where T : class
    {
        await EnsureConnectedAsync();

        if (_initialized && _connection != null)
        {
            var channel = await _connection.CreateChannelAsync();
            
            // Declare Main Exchange
            await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true);
            
            // Declare DLX Exchange
            var dlxExchange = $"{ExchangeName}.dlx";
            await channel.ExchangeDeclareAsync(dlxExchange, ExchangeType.Topic, durable: true);
            
            // Queue Args for DLX
            var args = new Dictionary<string, object?>
            {
                { "x-dead-letter-exchange", dlxExchange }
            };
            
            await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: args);
            await channel.QueueBindAsync(queue: queueName, exchange: ExchangeName, routingKey: routingKey);
            
            // Create Dead Letter Queue
            var dlqName = $"{queueName}.dead";
            await channel.QueueDeclareAsync(queue: dlqName, durable: true, exclusive: false, autoDelete: false);
            await channel.QueueBindAsync(queue: dlqName, exchange: dlxExchange, routingKey: routingKey);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var @event = JsonSerializer.Deserialize<T>(json);
                    if (@event != null)
                    {
                        await handler(@event);
                    }
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to process message from {Queue}", queueName);
                    await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                }
            };

            _consumerChannels.Add(channel);
            await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);
            _logger?.LogInformation("Subscribed to queue {Queue} with routing key {Key}", queueName, routingKey);
            return;
        }

        throw new InvalidOperationException("RabbitMQ connection is not established. Cannot subscribe to queue.");
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var ch in _consumerChannels)
        {
            try
            {
                await ch.CloseAsync();
                ch.Dispose();
            }
            catch { }
        }

        if (_publishChannel != null)
        {
            await _publishChannel.CloseAsync();
            _publishChannel.Dispose();
        }
        if (_connection != null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }
    }
}

