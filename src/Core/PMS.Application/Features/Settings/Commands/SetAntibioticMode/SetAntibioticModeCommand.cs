using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Settings.Commands.SetAntibioticMode;

/// <summary>
/// Changes how strictly this pharmacy captures antibiotic prescriptions. <b>Admin only</b>,
/// enforced by the endpoint's policy.
///
/// <para>Takes effect on the next sale. Nothing is recomputed for past sales and nothing should
/// be: a sale made under Off was correct under the rules in force when it happened. That is why
/// the register flags a missing prescription only under Required — see the module doc.</para>
/// </summary>
public sealed record SetAntibioticModeCommand(AntibioticPrescriptionMode Mode)
    : IRequest<Result<AntibioticModeDto>>, ITenantScopedRequest;
