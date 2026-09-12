using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Suppliers;

/// <summary>
/// Adds a supplier, or corrects one.
///
/// <para>One page for both, because the form is identical and two would drift. Editing restates
/// no history: purchases reference the supplier by id, so fixing a misspelled name updates every
/// screen that names them and changes nothing about what was bought.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class EditModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public EditModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "id")]
    public Guid? SupplierId { get; set; }

    [BindProperty]
    public SupplierInput Input { get; set; } = new();

    public bool IsEdit => SupplierId is not null;

    public string Title => IsEdit ? "Edit supplier" : "Add supplier";

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (SupplierId is not { } id)
        {
            return Page();
        }

        var result = await _api.GetSupplierAsync(id, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? NotFound();
        }

        var supplier = result.Value!;

        Input = new SupplierInput
        {
            Name = supplier.Name,
            Phone = supplier.Phone,
            ContactPerson = supplier.ContactPerson,
            Email = supplier.Email,
            Address = supplier.Address,
            Company = supplier.Company,
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var payload = new CreateSupplierPayload(
            Input.Name!, Input.Phone!, Input.ContactPerson,
            Input.Email, Input.Address, Input.Company);

        var result = SupplierId is { } id
            ? await _api.UpdateSupplierAsync(id, payload, ct)
            : await _api.CreateSupplierAsync(payload, ct);

        if (!result.IsSuccess)
        {
            // The API is the authoritative validator: its per-field messages land under the same
            // inputs the client-side hints would have flagged.
            return await HandleFailureAsync(result) ?? Page();
        }

        SuccessMessage = IsEdit
            ? $"{result.Value!.Name} updated."
            : $"{result.Value!.Name} added. You can record a purchase from them now.";

        return RedirectToPage("/Suppliers/Detail", new { id = result.Value!.Id });
    }

    public sealed class SupplierInput
    {
        [Required(ErrorMessage = "A supplier needs a name.")]
        [StringLength(200)]
        public string? Name { get; set; }

        [Required(ErrorMessage = "A phone number is required.")]
        [StringLength(40)]
        public string? Phone { get; set; }

        [StringLength(200)]
        public string? ContactPerson { get; set; }

        // Not [EmailAddress]: plenty of distributors use an internal address that would fail a
        // strict check, and the API applies the same loose rule for the same reason.
        [StringLength(256)]
        public string? Email { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [StringLength(200)]
        public string? Company { get; set; }
    }
}
