using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Auth.Commands.PlatformLogin;

/// <summary>Signs in a platform operator. No pharmacy is involved.</summary>
public sealed record PlatformLoginCommand(string Email, string Password)
    : IRequest<Result<AuthResultDto>>;
