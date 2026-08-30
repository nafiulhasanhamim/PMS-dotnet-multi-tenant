using System.Reflection;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.SharedKernel.Behaviors;

/// <summary>
/// Marker attribute for access logging. The actual attribute is defined in Application layer.
/// This interface allows the behavior to work without referencing Application directly.
/// </summary>
public interface IAccessLogAttribute
{
    string ActionName { get; }
    string EntityName { get; }
}

/// <summary>
/// Interface for responses that provide entity ID for access logging.
/// </summary>
public interface IAccessLoggable
{
    string? GetEntityId();
}

/// <summary>
/// Interface for access logger service. Implemented in Infrastructure layer.
/// </summary>
public interface IAccessLogger
{
    Task LogAsync(
        string action,
        string entityName,
        string? entityId = null,
        string? additionalInfo = null,
        bool isSuccess = true,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// MediatR pipeline behavior that logs access to entities for audit trail.
/// Only logs requests decorated with [AccessLog] attribute.
/// This should be the innermost behavior (runs after handler succeeds).
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class AccessLogBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAccessLogger? _accessLogger;
    private readonly ILogger<AccessLogBehavior<TRequest, TResponse>> _logger;

    public AccessLogBehavior(
        ILogger<AccessLogBehavior<TRequest, TResponse>> logger,
        IAccessLogger? accessLogger = null)
    {
        _logger = logger;
        _accessLogger = accessLogger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();

        // Check for AccessLog attribute
        var accessLogAttr = typeof(TRequest)
            .GetCustomAttributes()
            .FirstOrDefault(a => a.GetType().Name == "AccessLogAttribute");

        if (accessLogAttr == null || _accessLogger == null)
        {
            return response;
        }

        try
        {
            // Get action and entity name via reflection
            var actionName = accessLogAttr.GetType().GetProperty("ActionName")?.GetValue(accessLogAttr)?.ToString();
            var entityName = accessLogAttr.GetType().GetProperty("EntityName")?.GetValue(accessLogAttr)?.ToString();

            if (string.IsNullOrEmpty(actionName) || string.IsNullOrEmpty(entityName))
            {
                return response;
            }

            // Try to get entity ID from response
            string? entityId = null;
            if (response is IAccessLoggable loggable)
            {
                entityId = loggable.GetEntityId();
            }
            else if (response != null)
            {
                // Try to find GetEntityId method or Id property via reflection
                entityId = TryGetEntityId(response);
            }

            await _accessLogger.LogAsync(
                actionName,
                entityName,
                entityId,
                cancellationToken: cancellationToken);

            _logger.LogDebug(
                "AccessLog: {Action} {Entity} #{EntityId}",
                actionName,
                entityName,
                entityId ?? "N/A");
        }
        catch (Exception ex)
        {
            // Don't fail the request if access logging fails - just log warning
            _logger.LogWarning(
                ex,
                "Failed to write access log for {RequestName}",
                typeof(TRequest).Name);
        }

        return response;
    }

    private static string? TryGetEntityId(object response)
    {
        // Handle Result<T> pattern
        var valueProperty = response.GetType().GetProperty("Value");
        var actualValue = valueProperty?.GetValue(response) ?? response;

        if (actualValue == null) return null;

        // Try Id property
        var idProperty = actualValue.GetType().GetProperty("Id");
        if (idProperty != null)
        {
            var idValue = idProperty.GetValue(actualValue);
            return idValue?.ToString();
        }

        return null;
    }
}
