namespace EntitlementService.Models;

/// <summary>
/// An identity together with the party roles currently assigned to it.
/// </summary>
public record IdentityWithRoles(string Id, string Name, IReadOnlyList<PartyRole> Roles);
