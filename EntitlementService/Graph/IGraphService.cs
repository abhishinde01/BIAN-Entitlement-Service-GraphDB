using EntitlementService.Models;

namespace EntitlementService.Graph;

public interface IGraphService
{
    Task<bool> SeedDemoDataAsync();
    Task<EntitlementCheckResponse> CheckEntitlementAsync(string subject, string permissionName, string resourceId);
    Task<IReadOnlyList<IdentityWithRoles>> GetIdentitiesWithRolesAsync();
}
