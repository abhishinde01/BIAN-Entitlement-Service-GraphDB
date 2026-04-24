using EntitlementService.Graph;
using Microsoft.AspNetCore.Mvc;

namespace EntitlementService.Controllers;

/// <summary>
/// Developer utility for loading demo graph data into Neo4j.
/// Not intended for production use.
/// </summary>
[ApiController]
[Route("api/seed")]
[Produces("application/json")]
[ApiExplorerSettings(GroupName = "v1")]
public class SeedController(IGraphService graphService) : ControllerBase
{
    /// <summary>
    /// Seeds the Neo4j graph with demo Identities, PartyRoles, Entitlements, and their relationships.
    /// </summary>
    /// <remarks>
    /// Idempotent — existing demo nodes are wiped and recreated on each call.
    ///
    /// Seeded graph:
    /// - Alice (CUSTOMER-001) → ROLE-TRADER → execute-trade (Allow), view-account (Allow) on RESOURCE-PORTFOLIO-001
    /// - Bob (CUSTOMER-002) → ROLE-VIEWER → view-account (Allow), view-reports (Allow) on RESOURCE-PORTFOLIO-001
    /// - Charlie (CUSTOMER-003) → ROLE-ADMIN → manage-users (Allow) on RESOURCE-SYSTEM
    /// - Charlie (CUSTOMER-003) → ROLE-ADMIN → execute-trade (Deny) on RESOURCE-PORTFOLIO-001
    /// </remarks>
    /// <returns>A confirmation message on success.</returns>
    /// <response code="200">Demo data seeded successfully.</response>
    /// <response code="500">An error occurred while seeding — check Neo4j connectivity.</response>
    [HttpPost]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SeedDemoData()
    {
        try
        {
            await graphService.SeedDemoDataAsync();
            return Ok("Demo data seeded successfully");
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
}
