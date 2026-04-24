namespace EntitlementService.Models;

/// <summary>
/// Represents a role assigned to a party, aligned with the BIAN Party Role concept.
/// eg - ROLE-TRADER , ROLE-VIEWER, ROLE-ADMIN
/// </summary>
public record PartyRole(string RoleId, string RoleName, string Description);
