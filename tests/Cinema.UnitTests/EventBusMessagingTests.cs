using Cinema.Foundation.Messaging;
using Xunit;

namespace Cinema.UnitTests;

public class EventBusMessagingTests
{
    [Fact]
    public async Task EventBus_ShouldFailWithoutRabbitMQ_Publish()
    {
        var bus = new RabbitMqEventBus(hostName: "127.0.0.1", port: 59999, password: "test");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => bus.PublishAsync("test", new object()));
        Assert.Contains("RabbitMQ unavailable", ex.Message);
    }
    
    [Fact]
    public async Task EventBus_ShouldFailWithoutRabbitMQ_Subscribe()
    {
        var bus = new RabbitMqEventBus(hostName: "127.0.0.1", port: 59999, password: "test");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => bus.SubscribeAsync<object>("q", "test", async (e) => await Task.Delay(10)));
        Assert.Contains("RabbitMQ unavailable", ex.Message);
    }
}
