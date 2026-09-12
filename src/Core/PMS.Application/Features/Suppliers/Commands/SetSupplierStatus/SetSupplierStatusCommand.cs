using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Suppliers.Commands.SetSupplierStatus;

/// <summary>
/// Stops or resumes buying from a supplier. <b>Admin only</b>, and a soft delete either way.
///
/// <para>Deactivating hides them from the "new purchase" picker and from the Add Stock supplier
/// dropdown. It hides nothing that was already bought: the purchases, the payments and the
/// balance stay exactly where they were, because deactivating a supplier the pharmacy still owes
/// money to must not make that debt disappear.</para>
///
/// <para>One command with a flag rather than two, so the two paths cannot drift — the whole
/// operation is one property.</para>
/// </summary>
public sealed record SetSupplierStatusCommand(Guid SupplierId, bool IsActive)
    : IRequest<Result<SupplierDetailDto>>, ITenantScopedRequest;
