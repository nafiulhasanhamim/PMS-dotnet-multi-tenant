using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.Domain.ValueObjects;
using Mapster;

namespace PMS.Application.Common.DTOs;

/// <summary>
/// Data transfer object for Order entity.
/// </summary>
public class OrderDto : IMapFrom<Order>
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public DateTime OrderDateUtc { get; set; }
    public OrderStatus Status { get; set; }
    public AddressDto ShippingAddress { get; set; } = null!;
    public string? Notes { get; set; }
    public List<OrderItemDto> Items { get; set; } = [];
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = null!;
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }

    public void ConfigureMapping()
    {
        TypeAdapterConfig<Order, OrderDto>
            .NewConfig()
            .Map(dest => dest.CustomerName, src => src.Customer != null ? src.Customer.FullName : null)
            .Map(dest => dest.ShippingAddress, src => src.ShippingAddress)
            .Map(dest => dest.Items, src => src.Items)
            .Map(dest => dest.Subtotal, src => src.Subtotal.Amount)
            .Map(dest => dest.Total, src => src.Total.Amount)
            .Map(dest => dest.Currency, src => src.Total.Currency);
    }
}

/// <summary>
/// Summary DTO for order lists.
/// </summary>
public class OrderSummaryDto : IMapFrom<Order>
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public string? CustomerName { get; set; }
    public DateTime OrderDateUtc { get; set; }
    public OrderStatus Status { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = null!;
    public int ItemCount { get; set; }

    public void ConfigureMapping()
    {
        TypeAdapterConfig<Order, OrderSummaryDto>
            .NewConfig()
            .Map(dest => dest.CustomerName, src => src.Customer != null ? src.Customer.FullName : null)
            .Map(dest => dest.Total, src => src.Total.Amount)
            .Map(dest => dest.Currency, src => src.Total.Currency)
            .Map(dest => dest.ItemCount, src => src.Items.Count);
    }
}

/// <summary>
/// DTO for order line items.
/// </summary>
public class OrderItemDto : IMapFrom<OrderItem>
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = null!;

    public void ConfigureMapping()
    {
        TypeAdapterConfig<OrderItem, OrderItemDto>
            .NewConfig()
            .Map(dest => dest.UnitPrice, src => src.UnitPrice.Amount)
            .Map(dest => dest.Total, src => src.Total.Amount)
            .Map(dest => dest.Currency, src => src.UnitPrice.Currency);
    }
}

/// <summary>
/// DTO for address value object.
/// </summary>
public class AddressDto : IMapFrom<Address>
{
    public string Street { get; set; } = null!;
    public string City { get; set; } = null!;
    public string State { get; set; } = null!;
    public string PostalCode { get; set; } = null!;
    public string Country { get; set; } = null!;
}
