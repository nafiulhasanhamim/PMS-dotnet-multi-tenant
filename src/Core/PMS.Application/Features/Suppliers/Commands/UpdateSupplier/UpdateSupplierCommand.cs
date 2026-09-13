using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Suppliers.Commands.UpdateSupplier;

/// <summary>
/// Corrects a supplier's contact details.
///
/// <para>Changes nothing about what was bought from them. A purchase records the supplier by id,
/// so renaming one updates every screen that names them and restates no history — which is the
/// right behaviour for a typo, and the reason a supplier is never duplicated to fix one.</para>
/// </summary>
public sealed record UpdateSupplierCommand(
    Guid SupplierId,
    string Name,
    string Phone,
    string? ContactPerson,
    string? Email,
    string? Address,
    string? Company)
    : IRequest<Result<SupplierDetailDto>>, ITenantScopedRequest;
