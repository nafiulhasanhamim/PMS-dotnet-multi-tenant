using PMS.Application.Common.DTOs;
using PMS.Domain.Entities;
using PMS.Domain.ValueObjects;
using Mapster;

namespace PMS.Application.Common.Mappings;

/// <summary>
/// Extension methods for mapping domain entities to DTOs using Mapster.
/// These are convenience methods that use the registered Mapster configuration.
/// For handlers, prefer injecting IMapper for testability.
/// </summary>
public static class MappingExtensions
{
    /// <summary>
    /// Maps a Customer entity to CustomerDto.
    /// </summary>
    public static CustomerDto ToDto(this Customer customer)
        => customer.Adapt<CustomerDto>();

    /// <summary>
    /// Maps a Customer entity to CustomerSummaryDto.
    /// </summary>
    public static CustomerSummaryDto ToSummaryDto(this Customer customer)
        => customer.Adapt<CustomerSummaryDto>();

    /// <summary>
    /// Maps a Product entity to ProductDto.
    /// </summary>
    public static ProductDto ToDto(this Product product)
        => product.Adapt<ProductDto>();

    /// <summary>
    /// Maps a Product entity to ProductSummaryDto.
    /// </summary>
    public static ProductSummaryDto ToSummaryDto(this Product product)
        => product.Adapt<ProductSummaryDto>();

    /// <summary>
    /// Maps an Order entity to OrderDto.
    /// </summary>
    public static OrderDto ToDto(this Order order)
        => order.Adapt<OrderDto>();

    /// <summary>
    /// Maps an Order entity to OrderSummaryDto.
    /// </summary>
    public static OrderSummaryDto ToSummaryDto(this Order order)
        => order.Adapt<OrderSummaryDto>();

    /// <summary>
    /// Maps an OrderItem entity to OrderItemDto.
    /// </summary>
    public static OrderItemDto ToDto(this OrderItem item)
        => item.Adapt<OrderItemDto>();

    /// <summary>
    /// Maps an Address value object to AddressDto.
    /// </summary>
    public static AddressDto ToDto(this Address address)
        => address.Adapt<AddressDto>();
}
