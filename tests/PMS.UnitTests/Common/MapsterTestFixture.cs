using PMS.Application.Common.DTOs;
using PMS.Domain.Entities;
using Mapster;

namespace PMS.UnitTests.Common;

/// <summary>
/// Shared fixture for Mapster configuration in tests.
/// Initializes Mapster mappings once for all tests.
/// </summary>
public class MapsterTestFixture
{
    private static bool _initialized;
    private static readonly object _lock = new();

    public MapsterTestFixture()
    {
        Initialize();
    }

    public static void Initialize()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            var config = TypeAdapterConfig.GlobalSettings;

            // Configure Customer -> CustomerDto
            config.NewConfig<Customer, CustomerDto>()
                .Map(dest => dest.Email, src => src.Email.Value)
                .Map(dest => dest.PhoneNumber, src => src.PhoneNumber != null ? src.PhoneNumber.Value : null);

            // Configure Product -> ProductDto
            config.NewConfig<Product, ProductDto>()
                .Map(dest => dest.Price, src => src.Price.Amount)
                .Map(dest => dest.Currency, src => src.Price.Currency);

            _initialized = true;
        }
    }
}
