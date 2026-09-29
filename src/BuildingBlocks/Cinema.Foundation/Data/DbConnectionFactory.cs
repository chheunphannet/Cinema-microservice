using System;
using System.Data;
using Npgsql;

namespace Cinema.Foundation.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
    IDbConnection CreateReadConnection();
}

public class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _writeConnectionString;
    private readonly string _readConnectionString;

    public NpgsqlConnectionFactory(string? writeConnectionString, string? readConnectionString = null)
    {
        if (string.IsNullOrWhiteSpace(writeConnectionString))
            throw new ArgumentException("Write connection string is required.", nameof(writeConnectionString));

        _writeConnectionString = writeConnectionString;
        _readConnectionString = !string.IsNullOrWhiteSpace(readConnectionString) ? readConnectionString : _writeConnectionString;
    }

    public IDbConnection CreateConnection()
    {
        return new NpgsqlConnection(_writeConnectionString);
    }

    public IDbConnection CreateReadConnection()
    {
        return new NpgsqlConnection(_readConnectionString);
    }
}
