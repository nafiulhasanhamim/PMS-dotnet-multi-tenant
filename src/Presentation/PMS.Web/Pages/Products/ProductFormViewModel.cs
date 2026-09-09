using PMS.Web.Api;

namespace PMS.Web.Pages.Products;

/// <summary>
/// Everything the shared product form partial needs beyond the bound input: whether it is an
/// edit, where Cancel goes, the catalogue entry it was pre-filled from (which changes the
/// section headings and shows the confirm-the-antibiotic-flag prompt), and any form-level
/// error to show as a banner.
/// </summary>
public sealed record ProductFormViewModel(
    ProductFormInput Input,
    bool IsEdit,
    string CancelUrl,
    CatalogMedicineSearchItem? FromCatalog,
    string? FormError);
