---
name: dev-setup-workarounds
description: This skill should be used when the user asks about "dev setup", "local testing", "orval regeneration", "hardcoding permissions", "supervisor role", "UsersCreate", "canEnterForOthers", or "generate API client" in the context of the unified-scheduling project. Provides the standard workarounds needed to test features locally when the full auth/permission infrastructure is not yet wired up.
version: 1.0.0
---

# Dev Setup Workarounds — Unified Scheduling

These are the standard temporary changes needed to test features locally. All changes marked **REVERT BEFORE PR**.

---

## 1. Regenerating the Orval API Client

The devcontainer cannot reach `localhost:5000` on the host. Workaround:

**Step 1** — Update `web/orval.config.ts` to point at a local file (both `unified` and `unifiedZod` input targets):
```ts
input: {
  target: './openapi.json',  // was: 'http://localhost:5000/openapi/v1.json'
},
```

**Step 2** — From inside the devcontainer, fetch the spec and regenerate:
```bash
curl -s http://host.docker.internal:5000/openapi/v1.json -o openapi.json && npm run gen:api:clean
```

**Step 3** — Revert `orval.config.ts` and delete `openapi.json` after generation.

---

## 2. Adding an EF Core Migration

`dotnet ef` is not installed in the devcontainer. It IS installed in the API container. With the containers running:

```bash
docker exec -it unified-scheduling-api-1 bash -c "cd /opt/app-root/src && dotnet ef migrations add <MigrationName> --project db/db.csproj --startup-project api/Unified.Api/Unified.Api.csproj"
```

The generated files appear in `db/Migrations/` via the volume mount. Restart to apply:
```bash
./manage stop && ./manage debug
```

> **Why manual migration files don't work**: EF Core requires a `.Designer.cs` file alongside the migration that contains the `[Migration("timestamp_Name")]` attribute. Without it, EF Core cannot detect the migration as pending. Always use `dotnet ef migrations add` — never write migration files by hand.

---

## 3. Hardcoding Supervisor Permission (Frontend)

**File**: `web/src/modules/stats/views/EnterHoursView.vue`

```ts
// REVERT: const canEnterForOthers = computed(() => hasPermission(Permissions.StatsRecordsEnterForOthers));
const canEnterForOthers = computed(() => true);
```

This shows the "Employee" user picker so a supervisor can enter hours on behalf of other users.

---

## 4. Hardcoding UsersCreate Permission (Frontend)

**File**: `web/src/modules/myteam/views/Myteam.vue`

```html
<!-- REVERT: v-if="accessControl.hasPermission(Permissions.UsersCreate)" -->
<UaBtn v-if="true" @click="handleAddMember" ...>
```

This shows the "Add Member" button so you can create a user account in the DB.

---

## 5. Bypassing UsersCreate Authorization (Backend)

**File**: `api/Unified.UserManagement/Controllers/UsersController.cs`

```csharp
// REVERT: [Authorize(Policy = UserManagementPolicies.UsersCreate)]
[Authorize]
```

This allows any authenticated user to create a user record, needed when your own account doesn't exist in the DB yet (circular dependency — can't get permissions without a DB record).

---

## Root Cause: New User Has No DB Record

The `UnifiedClaimsTransformer` looks up the authenticated user's IDIR GUID in the `Users` table. If the user doesn't exist, no `UserId` claim is added → `UserInfo.userId` is null → stat records cannot be saved.

**Fix**: Use the My Team page (with workarounds #4 and #5 above) to add yourself as a user. After that, log out and back in so claims refresh.

---

## Checklist Before Raising a PR

- [ ] Revert `orval.config.ts` (restore `http://localhost:5000/openapi/v1.json`)
- [ ] Delete `web/openapi.json` if it exists
- [ ] Revert `canEnterForOthers` in `EnterHoursView.vue`
- [ ] Revert `v-if="true"` in `Myteam.vue`
- [ ] Revert `[Authorize]` back to `[Authorize(Policy = UserManagementPolicies.UsersCreate)]` in `UsersController.cs`
