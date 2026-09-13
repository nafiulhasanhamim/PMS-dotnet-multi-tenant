using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Suppliers.Commands.CreateSupplier;

/// <summary>
/// Adds a distributor the pharmacy buys from.
///
/// <para>Admin and Pharmacist both. A pharmacist taking a delivery from a supplier nobody has
/// recorded yet should be able to record it and get on with booking the stock in — making them
/// wait for an Admin would push them to the standalone Add Stock screen instead, and the
/// purchase would never be captured.</para>
/// </summary>
public sealed record CreateSupplierCommand(
    string Name,
    string Phone,
    string? ContactPerson,
    string? Email,
    string? Address,
    string? Company)
    : IRequest<Result<SupplierDetailDto>>, ITenantScopedRequest;
