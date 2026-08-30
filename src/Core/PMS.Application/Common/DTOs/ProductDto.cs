using PMS.Domain.Entities;
using Mapster;

namespace PMS.Application.Common.DTOs;

/// <summary>
/// Data transfer object for Product entity.
/// </summary>
public class ProductDto : IMapFrom<Product>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Sku { get; set; } = null!;
    public decimal Price { get; set; }
    public string Currency { get; set; } = null!;
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }

    public void ConfigureMapping()
    {
        TypeAdapterConfig<Product, ProductDto>
            .NewConfig()
            .Map(dest => dest.Price, src => src.Price.Amount)
            .Map(dest => dest.Currency, src => src.Price.Currency);
    }
}

/// <summary>
/// Summary DTO for product lists.
/// </summary>
public class ProductSummaryDto : IMapFrom<Product>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Sku { get; set; } = null!;
    public decimal Price { get; set; }
    public string Currency { get; set; } = null!;
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; }

    public void ConfigureMapping()
    {
        TypeAdapterConfig<Product, ProductSummaryDto>
            .NewConfig()
            .Map(dest => dest.Price, src => src.Price.Amount)
            .Map(dest => dest.Currency, src => src.Price.Currency);
    }
}
