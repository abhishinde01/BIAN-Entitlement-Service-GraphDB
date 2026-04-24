namespace EntitlementService.Models;

/// <summary>
/// DTO for requesting an entitlement check against a specific resource and permission.
/// </summary>
public record EntitlementCheckRequest(
    string Subject,
    string PermissionName,
    string ResourceId);
