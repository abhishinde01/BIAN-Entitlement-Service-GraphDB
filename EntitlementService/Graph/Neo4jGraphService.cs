using EntitlementService.Models;
using Neo4j.Driver;

namespace EntitlementService.Graph;

public sealed class Neo4jGraphService : IGraphService, IAsyncDisposable
{
    private readonly IDriver _driver;

    public Neo4jGraphService(IConfiguration configuration)
    {
        var uri      = configuration["Neo4j:Uri"]      ?? throw new InvalidOperationException("Neo4j:Uri is not configured.");
        var username = configuration["Neo4j:Username"] ?? throw new InvalidOperationException("Neo4j:Username is not configured.");
        var password = configuration["Neo4j:Password"] ?? throw new InvalidOperationException("Neo4j:Password is not configured.");

        _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));
    }

    // -------------------------------------------------------------------------
    // Seed
    // -------------------------------------------------------------------------

    public async Task<bool> SeedDemoDataAsync()
    {
        await using var session = _driver.AsyncSession();

        // Wipe existing demo data so re-seeding is idempotent
        await session.ExecuteWriteAsync(tx => tx.RunAsync("""
            MATCH (n)
            WHERE n:Identity OR n:PartyRole OR n:Entitlement
            DETACH DELETE n
            """));

        // --- Identities -------------------------------------------------------
        await session.ExecuteWriteAsync(tx => tx.RunAsync("""
            MERGE (:Identity {id: 'CUSTOMER-001', name: 'Alice'})
            MERGE (:Identity {id: 'CUSTOMER-002', name: 'Bob'})
            MERGE (:Identity {id: 'CUSTOMER-003', name: 'Charlie'})
            """));

        // --- PartyRoles -------------------------------------------------------
        await session.ExecuteWriteAsync(tx => tx.RunAsync("""
            MERGE (:PartyRole {roleId: 'ROLE-TRADER', roleName: 'Trader',
                               description: 'Can execute trades and view account details'})
            MERGE (:PartyRole {roleId: 'ROLE-VIEWER', roleName: 'Viewer',
                               description: 'Read-only access to accounts and reports'})
            MERGE (:PartyRole {roleId: 'ROLE-ADMIN',  roleName: 'Admin',
                               description: 'System administration and user management'})
            """));

        // --- Entitlements -----------------------------------------------------
        await session.ExecuteWriteAsync(tx => tx.RunAsync("""
            MERGE (:Entitlement {entitlementId: 'ENT-001', permissionName: 'execute-trade',
                                 resourceId: 'RESOURCE-PORTFOLIO-001', effect: 'Allow'})
            MERGE (:Entitlement {entitlementId: 'ENT-002', permissionName: 'view-account',
                                 resourceId: 'RESOURCE-PORTFOLIO-001', effect: 'Allow'})
            MERGE (:Entitlement {entitlementId: 'ENT-003', permissionName: 'view-reports',
                                 resourceId: 'RESOURCE-PORTFOLIO-001', effect: 'Allow'})
            MERGE (:Entitlement {entitlementId: 'ENT-004', permissionName: 'manage-users',
                                 resourceId: 'RESOURCE-SYSTEM',        effect: 'Allow'})
            MERGE (:Entitlement {entitlementId: 'ENT-005', permissionName: 'execute-trade',
                                 resourceId: 'RESOURCE-PORTFOLIO-001', effect: 'Deny'})
            """));

        // --- Relationships ----------------------------------------------------

        // Alice → ROLE-TRADER → execute-trade (Allow), view-account (Allow)
        await session.ExecuteWriteAsync(tx => tx.RunAsync("""
            MATCH (i:Identity  {id:      'CUSTOMER-001'})
            MATCH (r:PartyRole {roleId:  'ROLE-TRADER'})
            MATCH (e1:Entitlement {entitlementId: 'ENT-001'})
            MATCH (e2:Entitlement {entitlementId: 'ENT-002'})
            MERGE (i)-[:HAS_ROLE]->(r)
            MERGE (r)-[:HAS_ENTITLEMENT]->(e1)
            MERGE (r)-[:HAS_ENTITLEMENT]->(e2)
            """));

        // Bob → ROLE-VIEWER → view-account (Allow), view-reports (Allow)
        await session.ExecuteWriteAsync(tx => tx.RunAsync("""
            MATCH (i:Identity  {id:      'CUSTOMER-002'})
            MATCH (r:PartyRole {roleId:  'ROLE-VIEWER'})
            MATCH (e1:Entitlement {entitlementId: 'ENT-002'})
            MATCH (e2:Entitlement {entitlementId: 'ENT-003'})
            MERGE (i)-[:HAS_ROLE]->(r)
            MERGE (r)-[:HAS_ENTITLEMENT]->(e1)
            MERGE (r)-[:HAS_ENTITLEMENT]->(e2)
            """));

        // Charlie → ROLE-ADMIN → manage-users (Allow)
        //         → ROLE-ADMIN → execute-trade Deny node (explicit denial)
        await session.ExecuteWriteAsync(tx => tx.RunAsync("""
            MATCH (i:Identity  {id:      'CUSTOMER-003'})
            MATCH (r:PartyRole {roleId:  'ROLE-ADMIN'})
            MATCH (e1:Entitlement {entitlementId: 'ENT-004'})
            MATCH (e2:Entitlement {entitlementId: 'ENT-005'})
            MERGE (i)-[:HAS_ROLE]->(r)
            MERGE (r)-[:HAS_ENTITLEMENT]->(e1)
            MERGE (r)-[:HAS_ENTITLEMENT]->(e2)
            """));

        return true;
    }

    // -------------------------------------------------------------------------
    // Check
    // -------------------------------------------------------------------------

    public async Task<EntitlementCheckResponse> CheckEntitlementAsync(
        string subject, string permissionName, string resourceId)
    {
        await using var session = _driver.AsyncSession();

        // Deny wins: if any matching entitlement is Deny, that result is returned first.
        const string cypher = """
            MATCH (i:Identity {id: $subject})-[:HAS_ROLE]->(r:PartyRole)-[:HAS_ENTITLEMENT]->(e:Entitlement)
            WHERE e.permissionName = $permissionName AND e.resourceId = $resourceId
            WITH e.effect AS effect, e.entitlementId AS entitlementId, r.roleName AS roleName
            ORDER BY CASE WHEN effect = 'Deny' THEN 0 ELSE 1 END
            RETURN effect, entitlementId, roleName
            LIMIT 1
            """;

        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(cypher, new
            {
                subject,
                permissionName,
                resourceId
            });

            var records = await cursor.ToListAsync(record => new
            {
                Effect        = record["effect"].As<string>(),
                EntitlementId = record["entitlementId"].As<string>(),
                RoleName      = record["roleName"].As<string>()
            });

            return records.FirstOrDefault();
        });

        if (result is null)
            return new EntitlementCheckResponse(false, "No entitlement found", null);

        return result.Effect == "Allow"
            ? new EntitlementCheckResponse(
                true,
                $"Access granted via role '{result.RoleName}' (entitlement {result.EntitlementId})",
                permissionName)
            : new EntitlementCheckResponse(
                false,
                $"Access explicitly denied via role '{result.RoleName}' (entitlement {result.EntitlementId})",
                null);
    }

    // -------------------------------------------------------------------------
    // Identities with Roles
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<IdentityWithRoles>> GetIdentitiesWithRolesAsync()
    {
        await using var session = _driver.AsyncSession();

        const string cypher = """
            MATCH (i:Identity)
            OPTIONAL MATCH (i)-[:HAS_ROLE]->(r:PartyRole)
            RETURN i.id AS id, i.name AS name,
                   collect(CASE WHEN r IS NOT NULL
                           THEN {roleId: r.roleId, roleName: r.roleName, description: r.description}
                           END) AS roles
            ORDER BY i.id
            """;

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(cypher);

            return await cursor.ToListAsync(record =>
            {
                var roles = record["roles"].As<List<Dictionary<string, object>>>()
                    .Where(d => d is not null)
                    .Select(d => new PartyRole(
                        d["roleId"].As<string>(),
                        d["roleName"].As<string>(),
                        d["description"].As<string>()))
                    .ToList();

                return new IdentityWithRoles(
                    record["id"].As<string>(),
                    record["name"].As<string>(),
                    roles);
            });
        });
    }

    public async ValueTask DisposeAsync() => await _driver.DisposeAsync();
}
