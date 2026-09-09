using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;

namespace PMS.Web.Pages.Products;

/// <summary>
/// Shared behaviour for the three pages that write a product: create a medicine, create an
/// other item, and edit either.
///
/// <para>The reason this exists is <see cref="MapApiFieldsToForm"/>. The API's field names
/// are flat (<c>BrandName</c>, <c>BasePerMid</c>) while the form binds them under
/// <c>Input</c>, so without a translation the server's own validation messages would arrive
/// attached to nothing and render as a page-level banner — losing the one thing that makes
/// server-side validation usable, which is the message appearing under the input it is
/// about.</para>
/// </summary>
public abstract class ProductWritePageModel : PmsPageModel
{
    [BindProperty]
    public ProductFormInput Input { get; set; } = new();

    /// <summary>The catalogue entry this form was pre-filled from, if any.</summary>
    public CatalogMedicineSearchItem? FromCatalog { get; protected set; }

    protected abstract bool IsEdit { get; }

    protected abstract string CancelUrl { get; }

    public ProductFormViewModel FormModel =>
        new(Input, IsEdit, CancelUrl, FromCatalog, FormError);

    /// <summary>
    /// The API reports validation against its own property names; the form binds under
    /// <c>Input</c>. Both create and update commands use the same names, so one mapping
    /// serves every page here.
    /// </summary>
    protected override string ModelStateKeyFor(string apiField) => $"Input.{apiField}";

    /// <summary>
    /// A duplicate brand + strength comes back as a 409 with no <c>errors</c> dictionary. It
    /// is a problem with one field, so it renders under the name rather than as a banner.
    /// </summary>
    protected override string? ConflictField => nameof(ProductFormInput.BrandName);

    /// <summary>
    /// Client-side checks that mirror the server's, so an obvious mistake does not need a
    /// round trip. The API remains authoritative — anything it rejects lands on the same
    /// inputs through <see cref="PmsPageModel.ApplyProblem"/>.
    /// </summary>
    protected void ValidateLocally()
    {
        if (string.IsNullOrWhiteSpace(Input.BrandName))
        {
            ModelState.AddModelError("Input.BrandName", "Enter a name.");
        }

        if (string.IsNullOrWhiteSpace(Input.BaseUnitName))
        {
            ModelState.AddModelError("Input.BaseUnitName", "Enter the smallest unit you sell.");
        }

        if (Input.IsMedicine)
        {
            if (string.IsNullOrWhiteSpace(Input.GenericName))
            {
                ModelState.AddModelError("Input.GenericName", "A medicine needs a generic name.");
            }

            if (string.IsNullOrWhiteSpace(Input.DosageForm))
            {
                ModelState.AddModelError("Input.DosageForm", "A medicine needs a dosage form.");
            }
        }

        if (Input.HasMidUnit)
        {
            if (Input.BasePerMid is null)
            {
                ModelState.AddModelError(
                    "Input.BasePerMid", "Enter how many units are in one pack.");
            }
            else if (Input.BasePerMid <= 1)
            {
                ModelState.AddModelError(
                    "Input.BasePerMid", "A pack has to hold more than one unit.");
            }

            if (Input.PricePerMid is null)
            {
                ModelState.AddModelError("Input.PricePerMid", "Enter the price for this pack.");
            }
        }

        if (Input.HasLargeUnit)
        {
            if (Input.MidPerLarge is null)
            {
                ModelState.AddModelError(
                    "Input.MidPerLarge", "Enter how many are in one bulk pack.");
            }
            else if (Input.MidPerLarge <= 1)
            {
                ModelState.AddModelError(
                    "Input.MidPerLarge", "A bulk pack has to hold more than one.");
            }

            if (Input.PricePerLarge is null)
            {
                ModelState.AddModelError(
                    "Input.PricePerLarge", "Enter the price for the bulk pack.");
            }
        }

        if (Input.PricePerBase < 0)
        {
            ModelState.AddModelError("Input.PricePerBase", "A price cannot be negative.");
        }
    }

    /// <summary>Where to send someone after a successful save.</summary>
    protected IActionResult RedirectToDetail(Guid id, string message)
    {
        SuccessMessage = message;

        return RedirectToPage("/Products/Detail", new { id });
    }
}
