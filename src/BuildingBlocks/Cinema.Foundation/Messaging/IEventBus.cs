namespace Cinema.Foundation.Messaging;

public interface IEventBus
{
    Task PublishAsync<T>(string routingKey, T @event) where T : class;
    Task SubscribeAsync<T>(string queueName, string routingKey, Func<T, Task> handler) where T : class;
}
