using EntitlementService.Graph;
using EntitlementService.Models;
using Microsoft.AspNetCore.Mvc;

namespace EntitlementService.Controllers;

/// <summary>
/// Handles entitlement evaluation requests aligned with the BIAN Access Control service domain.
/// </summary>
[ApiController]
[Route("api/entitlement")]
[Produces("application/json")]
public class EntitlementController(IGraphService graphService) : ControllerBase
{
    /// <summary>
    /// Returns all identities together with the party roles assigned to each.
    /// </summary>
    /// <remarks>
    /// Identities with no roles assigned are included with an empty roles array.
    /// </remarks>
    /// <returns>A list of <see cref="IdentityWithRoles"/>.</returns>
    /// <response code="200">List returned successfully.</response>
    [HttpGet("identities")]
    [ProducesResponseType(typeof(IReadOnlyList<IdentityWithRoles>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IdentityWithRoles>>> GetIdentitiesWithRoles()
    {
        var result = await graphService.GetIdentitiesWithRolesAsync();
        return Ok(result);
    }

    /// <summary>
    /// Evaluates whether a subject (identity) holds the requested permission on a given resource.
    /// </summary>
    /// <remarks>
    /// Traverses the graph: Identity → HAS_ROLE → PartyRole → HAS_ENTITLEMENT → Entitlement.
    /// Returns the first matched entitlement effect (Allow or Deny).
    /// If no entitlement is found the response indicates access is not granted.
    /// </remarks>
    /// <param name="request">The entitlement check request containing subject, permission, and resource.</param>
    /// <returns>An <see cref="EntitlementCheckResponse"/> describing whether access is allowed.</returns>
    /// <response code="200">Entitlement evaluated — inspect <c>IsAllowed</c> for the decision.</response>
    /// <response code="400">Request body is missing or invalid.</response>
    [HttpPost("check")]
    [ProducesResponseType(typeof(EntitlementCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EntitlementCheckResponse>> CheckEntitlement(
        [FromBody] EntitlementCheckRequest request)
    {
        var response = await graphService.CheckEntitlementAsync(
            request.Subject,
            request.PermissionName,
            request.ResourceId);

        return Ok(response);
    }
}
