using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Mapster;

namespace PMS.Application.Common.DTOs;

/// <summary>
/// Data transfer object for Customer entity.
/// </summary>
public class CustomerDto : IMapFrom<Customer>
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public CustomerStatus Status { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }

    public void ConfigureMapping()
    {
        TypeAdapterConfig<Customer, CustomerDto>
            .NewConfig()
            .Map(dest => dest.Email, src => src.Email.Value)
            .Map(dest => dest.PhoneNumber, src => src.PhoneNumber != null ? src.PhoneNumber.Value : null);
    }
}

/// <summary>
/// Summary DTO for customer lists.
/// </summary>
public class CustomerSummaryDto : IMapFrom<Customer>
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public CustomerStatus Status { get; set; }
    public int OrderCount { get; set; }

    public void ConfigureMapping()
    {
        TypeAdapterConfig<Customer, CustomerSummaryDto>
            .NewConfig()
            .Map(dest => dest.Email, src => src.Email.Value)
            .Map(dest => dest.OrderCount, src => src.Orders.Count);
    }
}
