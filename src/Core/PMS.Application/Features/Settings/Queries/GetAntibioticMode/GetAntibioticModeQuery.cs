using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Settings.Queries.GetAntibioticMode;

/// <summary>
/// This pharmacy's antibiotic prescription mode.
///
/// <para>Readable by any tenant user, not just an Admin: the billing screen needs it on every
/// load to decide whether to render the prescription panel, and the register needs it to decide
/// whether a missing prescription is worth flagging. Only changing it is restricted.</para>
/// </summary>
public sealed record GetAntibioticModeQuery
    : IRequest<Result<AntibioticModeDto>>, ITenantScopedRequest;
