namespace EntitlementService.Models;

/// <summary>
/// DTO containing the outcome of an entitlement check, including the matched permission
/// when access is granted.
/// </summary>
public record EntitlementCheckResponse(
    bool IsAllowed,
    string Reason,
    string? GrantedPermission);
