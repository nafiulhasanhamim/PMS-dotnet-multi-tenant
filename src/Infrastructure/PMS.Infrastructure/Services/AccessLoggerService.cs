using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Behaviors;
using PMS.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PMS.Infrastructure.Services;

/// <summary>
/// Service for logging access/audit trail entries to the database.
/// </summary>
public sealed class AccessLoggerService : IAccessLoggerService, IAccessLogger
{
    private readonly IRepository<AccessLog, IApplicationDbContext> _accessLogRepository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AccessLoggerService> _logger;

    public AccessLoggerService(
        IRepository<AccessLog, IApplicationDbContext> accessLogRepository,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AccessLoggerService> logger)
    {
        _accessLogRepository = accessLogRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LogAsync(AccessLogEntry entry, CancellationToken cancellationToken = default)
    {
        await LogAsync(
            entry.Action,
            entry.EntityName,
            entry.EntityId,
            entry.AdditionalInfo,
            entry.IsSuccess,
            entry.ErrorMessage,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task LogAsync(
        string action,
        string entityName,
        string? entityId = null,
        string? additionalInfo = null,
        bool isSuccess = true,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;

            var accessLog = AccessLog.Create(
                action: action,
                entityName: entityName,
                entityId: entityId,
                userId: _currentUserService.UserId,
                userEmail: _currentUserService.Email,
                userRole: GetPrimaryRole(),
                requestUrl: GetRequestUrl(httpContext),
                httpMethod: httpContext?.Request.Method,
                ipAddress: GetClientIpAddress(httpContext),
                userAgent: httpContext?.Request.Headers.UserAgent.ToString(),
                additionalInfo: additionalInfo,
                isSuccess: isSuccess,
                errorMessage: errorMessage);

            await _accessLogRepository.AddAsync(accessLog, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "AccessLog saved: {Action} {Entity} #{EntityId} by {UserId} from {IP}",
                action,
                entityName,
                entityId ?? "N/A",
                _currentUserService.UserId ?? "Anonymous",
                accessLog.IpAddress ?? "Unknown");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save access log: {Action} {Entity} #{EntityId}",
                action,
                entityName,
                entityId ?? "N/A");
            // Don't rethrow - access logging should not fail the main operation
        }
    }

    private string? GetPrimaryRole()
    {
        var roles = _currentUserService.Roles.ToList();
        if (!roles.Any()) return null;

        // Return highest priority role
        var priorityRoles = new[] { "Admin", "Manager", "User" };
        foreach (var role in priorityRoles)
        {
            if (roles.Contains(role, StringComparer.OrdinalIgnoreCase))
                return role;
        }

        return roles.First();
    }

    private static string? GetRequestUrl(HttpContext? httpContext)
    {
        if (httpContext == null) return null;

        var request = httpContext.Request;
        return $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
    }

    private static string? GetClientIpAddress(HttpContext? httpContext)
    {
        if (httpContext == null) return null;

        // Check for forwarded header first (for reverse proxy scenarios)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').First().Trim();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }
}
