namespace PMS.Application.Interfaces;

/// <summary>
/// Marks a command or query that only makes sense inside a pharmacy.
///
/// <see cref="PMS.Application.Common.Behaviors.TenantValidationBehavior{TRequest,TResponse}"/>
/// refuses to run one of these without a resolved tenant, so a handler never has to check.
/// Platform-level requests simply do not carry the marker.
/// </summary>
public interface ITenantScopedRequest
{
}
