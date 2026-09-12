using Microsoft.AspNetCore.Authorization;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Reports;

/// <summary>
/// The reports landing page: one card per report.
///
/// <para>It makes no API call of its own. That is deliberate — a landing page that fetched eight
/// summaries would be the slowest page in the application and would fail entirely if any one of
/// them did. Each card describes its report and links to it.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class IndexModel : PmsPageModel
{
}
