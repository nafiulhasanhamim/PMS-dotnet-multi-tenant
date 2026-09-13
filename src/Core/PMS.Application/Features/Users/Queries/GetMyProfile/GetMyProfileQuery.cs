using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Users.Queries.GetMyProfile;

/// <summary>Who am I, and what am I here? Role is per pharmacy.</summary>
public sealed record GetMyProfileQuery
    : IRequest<Result<MyProfileDto>>, ITenantScopedRequest;
