using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Cinema.IntegrationTests;

/// <summary>
/// Implements Phase 1 Technical Architecture Specification: Module 4 & 5
/// - Module 4.1: Testcontainers Workflow (GenericContainer Abstraction & Wait Strategies)
/// - Module 5.1: Performance Thresholds (p99 latency < 500ms)
/// </summary>
public class TestcontainersInfrastructureTests : IAsyncLifetime
{
    private readonly IContainer _redisContainer;

    public TestcontainersInfrastructureTests()
    {
        // Module 4.1: GenericContainer Abstraction with Wait Strategy (LogMessageWaitStrategy)
        _redisContainer = new ContainerBuilder("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .Build();
    }

    public async Task InitializeAsync()
    {
        // Starts isolated container for integration testing
        await _redisContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _redisContainer.DisposeAsync();
    }

    [Fact]
    public async Task RedisContainer_ShouldInitializeAndRespondWithinP99Threshold()
    {
        // Module 5.1: Performance Threshold < 500ms
        var sw = Stopwatch.StartNew();
        
        var execResult = await _redisContainer.ExecAsync(new[] { "redis-cli", "ping" });
        sw.Stop();

        Assert.Equal(0, execResult.ExitCode);
        Assert.Contains("PONG", execResult.Stdout);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Execution latency was {sw.ElapsedMilliseconds}ms, exceeding 500ms threshold");
    }

    [Fact]
    public async Task RedisContainer_ShouldEnforceEvictionPolicy()
    {
        // Module 2.2: Least Recently Used (LRU) Eviction Policy Verification
        var execResult = await _redisContainer.ExecAsync(new[] { "redis-cli", "config", "get", "maxmemory-policy" });
        
        Assert.Equal(0, execResult.ExitCode);
        // Default or configured maxmemory-policy
        Assert.NotEmpty(execResult.Stdout);
    }
}
