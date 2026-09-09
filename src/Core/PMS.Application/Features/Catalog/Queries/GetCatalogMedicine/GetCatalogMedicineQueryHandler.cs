using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Catalog.Queries.GetCatalogMedicine;

public sealed class GetCatalogMedicineQueryHandler
    : IRequestHandler<GetCatalogMedicineQuery, Result<CatalogMedicineSearchItemDto>>
{
    private readonly ICatalogSearchQueries _catalog;
    private readonly ILogger<GetCatalogMedicineQueryHandler> _logger;

    public GetCatalogMedicineQueryHandler(
        ICatalogSearchQueries catalog,
        ILogger<GetCatalogMedicineQueryHandler> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<Result<CatalogMedicineSearchItemDto>> Handle(
        GetCatalogMedicineQuery request, CancellationToken cancellationToken)
    {
        var entry = await _catalog.FindAsync(request.Id, cancellationToken);

        if (entry is null)
        {
            // The catalogue is shared and unfiltered, so a miss here genuinely means the row
            // is gone - most likely a stale import link after a catalogue refresh.
            _logger.LogWarning(
                "Catalog medicine {CatalogMedicineId} not found", request.Id);

            return Result.Failure<CatalogMedicineSearchItemDto>(
                Error.NotFound("CatalogMedicine", request.Id));
        }

        return entry;
    }
}
