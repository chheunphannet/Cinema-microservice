using System;
using System.Data;
using Cinema.Foundation.Data;
using Npgsql;
using Xunit;

namespace Cinema.UnitTests;

/// <summary>
/// Verifies the Read/Write CQRS database separation behavior.
/// </summary>
public class DatabaseReplicationTests
{
    [Fact]
    public void Factory_ShouldUseDifferentConnectionStrings_WhenReadDbIsProvided()
    {
        // Arrange
        var writeConnString = "Host=postgres-primary;Database=cinema;";
        var readConnString = "Host=postgres-replica;Database=cinema;";

        // Act
        var factory = new NpgsqlConnectionFactory(writeConnString, readConnString);
        var writeConnection = factory.CreateConnection();
        var readConnection = factory.CreateReadConnection();

        // Assert
        Assert.Equal(writeConnString, writeConnection.ConnectionString);
        Assert.Equal(readConnString, readConnection.ConnectionString);
        Assert.NotEqual(writeConnection.ConnectionString, readConnection.ConnectionString);
    }

    [Fact]
    public void Factory_ShouldFallbackToWriteDb_WhenReadDbIsNotProvided()
    {
        // Arrange
        var writeConnString = "Host=postgres-primary;Database=cinema;";

        // Act
        var factory = new NpgsqlConnectionFactory(writeConnString, null);
        var writeConnection = factory.CreateConnection();
        var readConnection = factory.CreateReadConnection();

        // Assert
        Assert.Equal(writeConnString, writeConnection.ConnectionString);
        Assert.Equal(writeConnString, readConnection.ConnectionString);
        Assert.Equal(writeConnection.ConnectionString, readConnection.ConnectionString);
    }

    [Fact]
    public void Factory_ShouldThrowException_WhenWriteDbIsNotProvided()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new NpgsqlConnectionFactory(null));
        Assert.Throws<ArgumentException>(() => new NpgsqlConnectionFactory("   "));
    }
}
