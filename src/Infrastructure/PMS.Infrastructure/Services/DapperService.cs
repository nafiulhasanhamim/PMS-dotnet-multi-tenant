using System.Data;
using PMS.Application.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace PMS.Infrastructure.Services;

/// <summary>
/// Dapper service for executing raw SQL queries and stored procedures.
/// </summary>
public class DapperService : IDapperService
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    public DapperService(string connectionString, ILogger<DapperService> logger)
    {
        _connectionString = !string.IsNullOrEmpty(connectionString)
            ? connectionString
            : throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
        _logger = logger;
    }

    /// <summary>
    /// Protected constructor for typed DapperService&lt;TDb&gt;.
    /// </summary>
    protected DapperService(string connectionString, ILogger logger)
    {
        _connectionString = !string.IsNullOrEmpty(connectionString)
            ? connectionString
            : throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        object? param = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var connection = CreateConnection();
            var result = await connection.QueryAsync<T>(
                new CommandDefinition(sql, param, cancellationToken: ct));
            return result.AsList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query: {Sql}", TruncateSql(sql));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? param = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<T>(
                new CommandDefinition(sql, param, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query: {Sql}", TruncateSql(sql));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(
        string sql,
        object? param = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var connection = CreateConnection();
            return await connection.ExecuteAsync(
                new CommandDefinition(sql, param, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {Sql}", TruncateSql(sql));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> ExecuteStoredProcAsync<T>(
        string storedProcName,
        object? param = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("Executing stored procedure: {StoredProc}", storedProcName);

            await using var connection = CreateConnection();
            var result = await connection.QueryAsync<T>(
                new CommandDefinition(
                    storedProcName,
                    param,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: ct));

            return result.AsList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing stored procedure: {StoredProc}", storedProcName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T?> ExecuteStoredProcFirstOrDefaultAsync<T>(
        string storedProcName,
        object? param = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("Executing stored procedure: {StoredProc}", storedProcName);

            await using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<T>(
                new CommandDefinition(
                    storedProcName,
                    param,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing stored procedure: {StoredProc}", storedProcName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? param = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<T>(
                new CommandDefinition(sql, param, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scalar query: {Sql}", TruncateSql(sql));
            throw;
        }
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    private static string TruncateSql(string sql)
    {
        const int maxLength = 200;
        return sql.Length <= maxLength ? sql : sql[..maxLength] + "...";
    }
}

/// <summary>
/// Typed Dapper service for specific database connections.
/// </summary>
/// <typeparam name="TDb">Database marker interface.</typeparam>
public class DapperService<TDb> : DapperService, IDapperService<TDb>
    where TDb : class
{
    public DapperService(string connectionString, ILogger<DapperService<TDb>> logger)
        : base(connectionString, logger)
    {
    }
}
