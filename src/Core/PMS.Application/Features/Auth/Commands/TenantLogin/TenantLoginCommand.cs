using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Auth.Commands.TenantLogin;

/// <summary>
/// Signs in at one pharmacy. The domain decides which — that is what removes any
/// "select tenant" step for someone who works at several.
/// </summary>
public sealed record TenantLoginCommand(string DomainName, string Email, string Password)
    : IRequest<Result<AuthResultDto>>;
