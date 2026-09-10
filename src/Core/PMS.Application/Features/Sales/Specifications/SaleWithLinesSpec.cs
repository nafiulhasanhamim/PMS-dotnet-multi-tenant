using Ardalis.Specification;
using PMS.Domain.Entities;

namespace PMS.Application.Features.Sales.Specifications;

/// <summary>
/// One sale with its lines and every return already recorded against them.
///
/// <para><b>The returns are the reason this include list is not shorter.</b> Both write paths
/// need them: a cancellation must not restore stock that a return has already put back, and a
/// return has to know what a line has left before it takes any more. Loading the lines alone
/// would make <c>SaleLine.ReturnedInBaseUnits</c> read zero — quietly, with no error — and both
/// of those decisions would then be made on the wrong number.</para>
/// </summary>
public sealed class SaleWithLinesSpec : SingleResultSpecification<Sale>
{
    public SaleWithLinesSpec(Guid saleId)
    {
        Query
            .Where(sale => sale.Id == saleId)
            .Include(sale => sale.Lines)
            .ThenInclude(line => line.Returns);
    }
}
