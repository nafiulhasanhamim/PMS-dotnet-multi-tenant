using PMS.Application.Common.DTOs;
using PMS.Application.Features.Customers.Commands.CreateCustomer;
using PMS.Application.Features.Customers.Commands.DeleteCustomer;
using PMS.Application.Features.Customers.Commands.UpdateCustomer;
using PMS.Application.Features.Customers.Queries.GetCustomerById;
using PMS.Application.Features.Customers.Queries.GetCustomers;
using PMS.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace PMS.WebApi.Controllers;

/// <summary>
/// API endpoints for customer management.
/// </summary>
public class CustomersController : ApiControllerBase
{
    /// <summary>
    /// Gets a paginated list of customers.
    /// </summary>
    /// <param name="searchTerm">Optional search term for filtering.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Page size (default: 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of customers.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(GetCustomersResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] string? searchTerm = null,
        [FromQuery] CustomerStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetCustomersQuery(searchTerm, status, page, pageSize);
        var result = await Mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a customer by ID.
    /// </summary>
    /// <param name="id">The customer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The customer details.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomer(Guid id, CancellationToken cancellationToken = default)
    {
        var query = new GetCustomerByIdQuery(id);
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Creates a new customer.
    /// </summary>
    /// <param name="request">The customer creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created customer.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCustomer(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateCustomerCommand(
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber);

        var result = await Mediator.Send(command, cancellationToken);
        return HandleCreatedResult(result, nameof(GetCustomer), c => new { id = c.Id });
    }

    /// <summary>
    /// Updates an existing customer.
    /// </summary>
    /// <param name="id">The customer ID.</param>
    /// <param name="request">The customer update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated customer.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCustomer(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateCustomerCommand(
            id,
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber);

        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Deletes a customer (soft delete).
    /// </summary>
    /// <param name="id">The customer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content if successful.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCustomer(Guid id, CancellationToken cancellationToken = default)
    {
        var command = new DeleteCustomerCommand(id);
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult(result);
    }
}

/// <summary>
/// Request model for creating a customer.
/// </summary>
public sealed record CreateCustomerRequest(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber = null);

/// <summary>
/// Request model for updating a customer.
/// </summary>
public sealed record UpdateCustomerRequest(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber = null);
