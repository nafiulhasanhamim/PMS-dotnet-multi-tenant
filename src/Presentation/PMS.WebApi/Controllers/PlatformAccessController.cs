using PMS.WebApi.Access;
using PMS.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PMS.WebApi.Controllers;

/// <summary>
/// Who can do what, read out of this running build.
///
/// <para><b>Platform admin only.</b> A complete map of the authorization model is exactly the
/// document somebody probing the system would like, and a pharmacy Admin has no use for it —
/// their own staff page already shows the three roles they can hand out.</para>
///
/// <para>A GET with no parameters and no side effects. It reflects the deployed build rather
/// than any pharmacy's data, so it is the same answer for every operator and carries nothing
/// tenant-scoped.</para>
/// </summary>
[Route("api/platform/access")]
[Authorize(Policy = AuthenticationExtensions.PlatformAdminPolicy)]
public class PlatformAccessController : ApiControllerBase
{
    private readonly AccessMatrixBuilder _matrix;

    public PlatformAccessController(AccessMatrixBuilder matrix)
    {
        _matrix = matrix;
    }

    /// <summary>The role-by-endpoint matrix, plus the fields withheld inside responses.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(AccessMatrixDto), StatusCodes.Status200OK)]
    public IActionResult GetAccessMatrix() => Ok(_matrix.Build());
}
