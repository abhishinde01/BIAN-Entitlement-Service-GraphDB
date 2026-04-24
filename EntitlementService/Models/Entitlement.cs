namespace EntitlementService.Models;

/// <summary>
/// Represents a permission grant or denial for a resource, aligned with the BIAN
/// Entitlement concept within the Access Control service domain.
/// Effect must be either "Allow" or "Deny".
/// eg - execute-trade, view-account, manage-users, view-reports
/// </summary>
public record Entitlement(
    string EntitlementId,
    string PermissionName,
    string ResourceId,
    string Effect);
