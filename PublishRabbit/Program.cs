using System;
using System.Text;
using RabbitMQ.Client;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var factory = new ConnectionFactory { HostName = "localhost", Port = 5672, UserName = "cinema_app", Password = "change-this-development-password" };
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        string message = "{\"CustomerEmail\":\"john.doe@example.com\",\"TotalAmount\":50.0,\"OrderId\":\"12345678-1234-1234-1234-123456789012\",\"VoucherCode\":\"WELCOME-5OFF\"}";
        var body = Encoding.UTF8.GetBytes(message);

        var props = new BasicProperties();
        await channel.BasicPublishAsync(exchange: "cinema.events", routingKey: "order.cancelled", mandatory: true, basicProperties: props, body: body);
        
        Console.WriteLine("Published order.cancelled");
    }
}
