namespace PMS.Application.Interfaces;

/// <summary>
/// Dapper service for executing raw SQL queries and stored procedures.
/// Use this for complex queries, reporting, or when EF Core is not suitable.
/// Registered via AddDapper() in DI configuration.
/// </summary>
public interface IDapperService
{
    /// <summary>
    /// Executes a query and returns a list of results.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="sql">The SQL query.</param>
    /// <param name="param">Optional query parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of results.</returns>
    Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        object? param = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a query and returns the first result or default.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="sql">The SQL query.</param>
    /// <param name="param">Optional query parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The first result or default.</returns>
    Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? param = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a non-query command (INSERT, UPDATE, DELETE).
    /// </summary>
    /// <param name="sql">The SQL command.</param>
    /// <param name="param">Optional command parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> ExecuteAsync(
        string sql,
        object? param = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a stored procedure and returns a list of results.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="storedProcName">The stored procedure name.</param>
    /// <param name="param">Optional stored procedure parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of results.</returns>
    Task<IReadOnlyList<T>> ExecuteStoredProcAsync<T>(
        string storedProcName,
        object? param = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a stored procedure and returns the first result or default.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="storedProcName">The stored procedure name.</param>
    /// <param name="param">Optional stored procedure parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The first result or default.</returns>
    Task<T?> ExecuteStoredProcFirstOrDefaultAsync<T>(
        string storedProcName,
        object? param = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a scalar query and returns a single value.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="sql">The SQL query.</param>
    /// <param name="param">Optional query parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The scalar result or default.</returns>
    Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? param = null,
        CancellationToken ct = default);
}

/// <summary>
/// Typed Dapper service for specific database connections.
/// Use database marker interfaces to target different databases.
/// </summary>
/// <typeparam name="TDb">Database marker interface (e.g., IApplicationDbContext, IReportingDbContext).</typeparam>
/// <example>
/// // Main database access (default)
/// public class MyHandler(IDapperService dapper) { }
///
/// // Reporting database access
/// public class ReportHandler(IDapperService&lt;IReportingDbContext&gt; reportingDapper) { }
/// </example>
public interface IDapperService<TDb> : IDapperService
    where TDb : class;
