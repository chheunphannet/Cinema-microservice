using System;
using RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
namespace Test {
    class Program {
        static async System.Threading.Tasks.Task Main() {
            var factory = new ConnectionFactory { HostName = "localhost" };
            var conn = await factory.CreateConnectionAsync();
            var ch = await conn.CreateChannelAsync();
            await ch.ConfirmSelectAsync();
        }
    }
}
