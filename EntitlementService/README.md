# BIAN Entitlement Service

## 1. Overview

The Entitlement Service evaluates whether a given identity (a party) holds a specific permission on a named resource. 
It implements the **Access Control** service domain from the [BIAN](https://bian.org) (Banking Industry Architecture Network) standard, 
which defines a canonical model for how banks govern who can do what on which resource.

Rather than a flat ACL table, permissions are modelled as a property graph stored in **Neo4j**. 
A query traverses the graph at runtime — Identity → role → entitlement — and returns an explicit `Allow` or `Deny` decision along with the reason. 
This makes the permission model auditable, flexible, and easy to extend without schema migrations.

---

## 2. Architecture

### Graph Model

Permissions are represented by three node types connected by two relationship types:

```
(:Identity) -[:HAS_ROLE]-> (:PartyRole) -[:HAS_ENTITLEMENT]-> (:Entitlement)
```

**Node properties**

| Node | Properties |
|---|---|
| `Identity` | `id` (e.g. `CUSTOMER-001`), `name` (e.g. `Alice`) |
| `PartyRole` | `roleId` (e.g. `ROLE-TRADER`), `roleName`, `description` |
| `Entitlement` | `entitlementId` (e.g. `ENT-001`), `permissionName` (e.g. `execute-trade`), `resourceId` (e.g. `RESOURCE-PORTFOLIO-001`), `effect` (`Allow` or `Deny`) |

**Check query** (`Neo4jGraphService.CheckEntitlementAsync`)

```cypher
MATCH (i:Identity {id: $subject})-[:HAS_ROLE]->(r:PartyRole)-[:HAS_ENTITLEMENT]->(e:Entitlement)
WHERE e.permissionName = $permissionName AND e.resourceId = $resourceId
RETURN e.effect AS effect, e.entitlementId AS entitlementId, r.roleName AS roleName
LIMIT 1
```

The service returns `IsAllowed = true` when `effect = "Allow"`, 
`IsAllowed = false` for an explicit `Deny`, 
and `IsAllowed = false` with reason `"No entitlement found"` when no matching path exists.

### API Endpoints

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/entitlement/check` | Evaluate a permission request |
| `POST` | `/api/seed` | Wipe and re-seed the demo graph |

### Project Layout

```
EntitlementService/
├── Controllers/          # EntitlementController, SeedController
├── Graph/                # IGraphService interface, Neo4jGraphService
├── Models/               # Identity, PartyRole, Entitlement, request/response DTOs
├── EntitlementService.Tests/
│   └── EntitlementCheckTests.cs   # xUnit + Moq unit tests
├── appsettings.json
└── Program.cs
```

---

## 3. Prerequisites

| Tool | Version |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0 |
| [Neo4j Desktop](https://neo4j.com/download/) | 5.x |
| Azure Subscription | — |
| Azure Key Vault | — |

---

## 4. Neo4j Setup

1. **Install Neo4j Desktop** from [neo4j.com/download](https://neo4j.com/download/).

2. **Create a new project** inside Neo4j Desktop.

3. **Add a local DBMS** — click *Add* → *Local DBMS*. Set the name to anything you like.

4. **Set the password** (update `appsettings.json` to match whatever you choose).

5. **Start the DBMS** by clicking the *Start* button. Wait until the status shows *Running*.

6. **Verify connectivity** — click *Open* to launch Neo4j Browser and run:
   ```cypher
   RETURN 1
   ```
   A result of `1` confirms the database is reachable on `bolt://localhost:7687`.

---

## 5. Configuration

Neo4j credentials are no longer stored in `appsettings.json`. 
They are kept in **Azure Key Vault** and loaded at startup. 
The local config files only hold non-secret settings and the Key Vault URI.

### Key Vault secrets

Store the following secrets in your Key Vault (use `--` as the hierarchy separator, which the provider maps back to `:`):

| Secret name | Example value |
|---|---|
| `Neo4j--Uri` | `bolt://localhost:7687` |
| `Neo4j--Username` | `neo4j` |
| `Neo4j--Password` | `your-password` |

### appsettings.json

```json
"KeyVault": {
  "Uri": "https://<your-vault-name>.vault.azure.net/"
}
```

### appsettings.Development.json

In development the service authenticates to Key Vault using a **Client Secret credential**. 
Add the following block and keep this file out of source control:

```json
"KeyVault": {
  "Uri": "https://<your-vault-name>.vault.azure.net/"
},
"AzureAd": {
  "TenantId": "<tenant-id>",
  "ClientId": "<client-id>",
  "ClientSecret": "<client-secret>"
}
```

> **Note:** `appsettings.Development.json` must be added to `.gitignore` — it contains credentials that should never be committed.

### Production

In production the service uses **System-Assigned Managed Identity** (`ManagedIdentityCredential`). 
grant the managed identity `Key Vault Secrets User` RBAC role on the vault.

The `IDriver` singleton is registered in `Program.cs` and injected into `Neo4jGraphService`.

---

## 6. Running the Service

```bash
cd EntitlementService
dotnet run
```

The service starts on:

- HTTP  — `http://localhost:5279`
- HTTPS — `https://localhost:7192`

Swagger UI (development only): `https://localhost:7192/swagger`

---

## 7. Seeding Demo Data

Populate the graph with three identities, three roles, and five entitlements:

```bash
curl -s -X POST http://localhost:5279/api/seed
```

Expected response:

```
Demo data seeded successfully.
```

The seed operation is idempotent — running it again wipes the existing demo nodes and recreates them cleanly.

**Seeded relationships**

| Identity | Role | Entitlements |
|---|---|---|
| Alice (`CUSTOMER-001`) | `ROLE-TRADER` | `execute-trade` Allow, `view-account` Allow |
| Bob (`CUSTOMER-002`) | `ROLE-VIEWER` | `view-account` Allow, `view-reports` Allow |
| Charlie (`CUSTOMER-003`) | `ROLE-ADMIN` | `manage-users` Allow, `execute-trade` **Deny** |

---

## 8. Testing Entitlement Check

### a. Alice checks execute-trade (should Allow)

Alice holds `ROLE-TRADER`, which has an `Allow` entitlement for `execute-trade` on `RESOURCE-PORTFOLIO-001`.

```bash
curl -s -X POST http://localhost:5279/api/entitlement/check \
  -H "Content-Type: application/json" \
  -d '{
    "subject": "CUSTOMER-001",
    "permissionName": "execute-trade",
    "resourceId": "RESOURCE-PORTFOLIO-001"
  }'
```

Expected response:

```json
{
  "isAllowed": true,
  "reason": "Access granted via role 'Trader' (entitlement ENT-001)",
  "grantedPermission": "execute-trade"
}
```

---

### b. Bob checks execute-trade (should Deny — wrong role)

Bob holds `ROLE-VIEWER`, which has no entitlement for `execute-trade`. No matching path is found in the graph.

```bash
curl -s -X POST http://localhost:5279/api/entitlement/check \
  -H "Content-Type: application/json" \
  -d '{
    "subject": "CUSTOMER-002",
    "permissionName": "execute-trade",
    "resourceId": "RESOURCE-PORTFOLIO-001"
  }'
```

Expected response:

```json
{
  "isAllowed": false,
  "reason": "No entitlement found",
  "grantedPermission": null
}
```

---

### c. Charlie checks manage-users (should Allow)

Charlie holds `ROLE-ADMIN`, which has an `Allow` entitlement for `manage-users` on `RESOURCE-SYSTEM`.

```bash
curl -s -X POST http://localhost:5279/api/entitlement/check \
  -H "Content-Type: application/json" \
  -d '{
    "subject": "CUSTOMER-003",
    "permissionName": "manage-users",
    "resourceId": "RESOURCE-SYSTEM"
  }'
```

Expected response:

```json
{
  "isAllowed": true,
  "reason": "Access granted via role 'Admin' (entitlement ENT-004)",
  "grantedPermission": "manage-users"
}
```

---

## 9. Running Unit Tests

```bash
cd EntitlementService
dotnet test EntitlementService.Tests/EntitlementService.Tests.csproj
```

The test suite uses **xUnit** and **Moq**. `IGraphService` is mocked so no Neo4j connection is required. 
All five tests instantiate `EntitlementController` directly and assert on `IsAllowed` and `Reason`.

| Test | Subject | Permission | Resource | Expected |
|---|---|---|---|---|
| `Test_Allow_WhenValidSubjectHasPermission` | CUSTOMER-001 | execute-trade | RESOURCE-PORTFOLIO-001 | `true` |
| `Test_Deny_WhenSubjectHasDenyEntitlement` | CUSTOMER-003 | execute-trade | RESOURCE-PORTFOLIO-001 | `false` |
| `Test_Deny_WhenSubjectHasNoEntitlement` | CUSTOMER-999 | any | any | `false` |
| `Test_Deny_WhenWrongResource` | CUSTOMER-002 | execute-trade | RESOURCE-PORTFOLIO-999 | `false` |
| `Test_Allow_AdminCanManageUsers` | CUSTOMER-003 | manage-users | RESOURCE-SYSTEM | `true` |

---

## 10. Graph Model Diagram

```
                        HAS_ROLE                  HAS_ENTITLEMENT
  (Identity)  ─────────────────────►  (PartyRole)  ──────────────────────►  (Entitlement)


Demo data
─────────────────────────────────────────────────────────────────────────────────────────────────────────────────

  Alice (CUSTOMER-001) ──HAS_ROLE──► ROLE-TRADER ──HAS_ENTITLEMENT──► ENT-001  execute-trade  PORTFOLIO-001  Allow
                                                 └─HAS_ENTITLEMENT──► ENT-002  view-account   PORTFOLIO-001  Allow

  Bob   (CUSTOMER-002) ──HAS_ROLE──► ROLE-VIEWER ──HAS_ENTITLEMENT──► ENT-002  view-account   PORTFOLIO-001  Allow
                                                 └─HAS_ENTITLEMENT──► ENT-003  view-reports   PORTFOLIO-001  Allow

Charlie (CUSTOMER-003) ──HAS_ROLE──► ROLE-ADMIN  ──HAS_ENTITLEMENT──► ENT-004  manage-users   RESOURCE-SYSTEM  Allow
                                                 └─HAS_ENTITLEMENT──► ENT-005  execute-trade  PORTFOLIO-001  Deny
```
