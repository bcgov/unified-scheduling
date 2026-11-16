using Microsoft.EntityFrameworkCore;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;

namespace Unified.DataMigration.Services;

public sealed class SsMyTeamWriter(UnifiedDbContext db, SsLeaveWriter leaveWriter)
{
    public static readonly string[] Tables =
    [
        "Region",
        "Location",
        "LookupCode",
        "LookupSortOrder",
        "Role",
        "User",
        "UserRole",
        "SheriffActingRank",
        "SheriffAwayLocation",
        "SheriffLeave",
    ];

    public async Task WriteAsync(
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession session,
        DateTimeOffset boundary,
        CancellationToken ct
    )
    {
        var duplicateRegions = rows["Region"]
            .GroupBy(x => x.Text("Name"), StringComparer.Ordinal)
            .Where(x => x.Count() > 1)
            .SelectMany(x => x)
            .Select(x => x.Id)
            .ToHashSet();
        foreach (var row in rows["Region"])
            await session.RowAsync(
                "Region",
                row,
                async () =>
                {
                    if (duplicateRegions.Contains(row.Id))
                        throw new SsReject("DuplicateSourceName");
                    var name = row.Text("Name") ?? throw new SsReject("MissingName");
                    var justin = row.Int("JustinId");
                    var hash = SsWriteSession.Hash(new { justin, Expiry = row.Date("ExpiryDate") });
                    if (await session.ReplayAsync("Region", row, hash, true, ct))
                        return;
                    var candidates = await db
                        .Regions.Where(x =>
                            justin != null ? x.JustinId == justin : x.JustinId == null && x.Name == name
                        )
                        .ToListAsync(ct);
                    if (candidates.Count > 1)
                        throw new SsReject("AmbiguousRegion");
                    var target = candidates.SingleOrDefault();
                    var created = target is null;
                    if (target is null)
                    {
                        target = session.Stamp(
                            new Region
                            {
                                Name = name,
                                JustinId = justin,
                                ExpiryDate = row.Date("ExpiryDate"),
                            }
                        );
                        db.Regions.Add(target);
                        await db.SaveChangesAsync(ct);
                    }
                    await session.LinkAsync("Region", row, "Region", target.Id, hash, created, ct);
                },
                ct
            );

        var pending = rows["Location"].ToList();
        var allIds = pending.Select(x => x.Id).ToHashSet();
        var visited = new HashSet<string>();
        while (pending.Count > 0)
        {
            var ready = pending
                .Where(x =>
                    x.Int("ParentLocationId") is not { } p
                    || !allIds.Contains(p.ToString())
                    || visited.Contains(p.ToString())
                )
                .ToList();
            if (ready.Count == 0)
            {
                foreach (var row in pending)
                    session.Outcome("Location", row.Id, "Rejected", "CyclicLocationParent");
                break;
            }
            foreach (var row in ready)
            {
                pending.Remove(row);
                visited.Add(row.Id);
                await session.RowAsync(
                    "Location",
                    row,
                    async () =>
                    {
                        var agency = row.Text("AgencyId");
                        if (string.IsNullOrWhiteSpace(agency))
                            throw new SsReject("MissingAgencyId");
                        if (rows["Location"].Count(x => x.Text("AgencyId") == agency) != 1)
                            throw new SsReject("DuplicateAgencyId");
                        var hash = SsWriteSession.Hash(
                            new
                            {
                                agency,
                                Parent = row.Int("ParentLocationId"),
                                Region = row.Int("RegionId"),
                            }
                        );
                        var parent = await session.ResolveIntAsync("Location", row.Int("ParentLocationId"), ct);
                        var region = await session.ResolveIntAsync("Region", row.Int("RegionId"), ct);
                        if (await session.ReplayAsync("Location", row, hash, true, ct))
                            return;
                        var targets = await db.Locations.Where(x => x.AgencyId == agency).ToListAsync(ct);
                        if (targets.Count > 1)
                            throw new SsReject("AmbiguousLocation");
                        var target = targets.SingleOrDefault();
                        var created = target is null;
                        if (target is null)
                        {
                            target = session.Stamp(
                                new Location
                                {
                                    AgencyId = agency,
                                    Name = row.Text("Name") ?? throw new SsReject("MissingName"),
                                    JustinLocationCode = row.Text("JustinCode"),
                                    ParentLocationId = parent,
                                    RegionId = region,
                                    ExpiryDate = row.Date("ExpiryDate"),
                                    Timezone = SsWriteSession.Timezone(row, true)!,
                                }
                            );
                            db.Locations.Add(target);
                            await db.SaveChangesAsync(ct);
                        }
                        await session.LinkAsync("Location", row, "Location", target.Id, hash, created, ct);
                    },
                    ct
                );
            }
        }
        foreach (var row in rows["LookupCode"].Where(x => x.Int("Type") == 7).OrderBy(x => x.Int("Id")))
            await session.RowAsync(
                "PositionType",
                row,
                async () =>
                {
                    var code = row.Text("Code");
                    if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
                        throw new SsReject("InvalidCode");
                    var description = row.Text("Description") ?? "";
                    if (description.Length > 100)
                        throw new SsReject("DescriptionTooLong");
                    if (rows["LookupCode"].Count(x => x.Int("Type") == 7 && x.Text("Code") == code) != 1)
                        throw new SsReject("DuplicateCode");
                    // Location/SubCode are deliberately absent from Type-7 identity and validation.
                    var hash = SsWriteSession.Hash(
                        new
                        {
                            code,
                            Effective = row.Date("EffectiveDate"),
                            Expiry = row.Date("ExpiryDate"),
                        }
                    );
                    if (await session.ReplayAsync("PositionType", row, hash, true, ct))
                        return;
                    await db.Database.ExecuteSqlRawAsync(
                        "LOCK TABLE \"PositionTypes\" IN SHARE ROW EXCLUSIVE MODE",
                        ct
                    );
                    var targets = await db.PositionTypes.Where(x => x.Code == code).ToListAsync(ct);
                    if (targets.Count > 1)
                        throw new SsReject("AmbiguousPosition");
                    var target = targets.SingleOrDefault();
                    var created = target is null;
                    if (target is null)
                    {
                        var max = await db.PositionTypes.Select(x => (int?)x.DisplayOrder).MaxAsync(ct);
                        if (max == int.MaxValue)
                            throw new SsReject("OrderOverflow");
                        target = session.Stamp(
                            new PositionType
                            {
                                Code = code,
                                Description = description,
                                EffectiveDate = row.Date("EffectiveDate") ?? session.Now,
                                ExpiryDate = row.Date("ExpiryDate"),
                                DisplayOrder = max is null ? 1 : max.Value + 1,
                            }
                        );
                        db.PositionTypes.Add(target);
                        await db.SaveChangesAsync(ct);
                    }
                    await session.LinkAsync("PositionType", row, "PositionType", target.Id, hash, created, ct);
                },
                ct
            );

        await leaveWriter.TypesAsync(rows["LookupCode"], session, ct);
        foreach (var row in rows["Role"])
            await session.RowAsync(
                "Role",
                row,
                async () =>
                {
                    var sourceName = row.Text("Name") ?? throw new SsReject("MissingRoleName");
                    var name = sourceName switch
                    {
                        "SuperAdmin" or "Super Admin" => "Administrator",
                        "Administrator" => "Sheriff Administrator",
                        "Manager" => "Manager",
                        "Sheriff" => "Staff",
                        _ => sourceName,
                    };
                    var hash = SsWriteSession.Hash(new { name });
                    if (await session.ReplayAsync("Role", row, hash, true, ct))
                        return;
                    var targets = (await db.Roles.Where(x => x.DeletedOn == null).ToListAsync(ct))
                        .Where(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (targets.Count > 1)
                        throw new SsReject("AmbiguousRole");
                    var target = targets.SingleOrDefault();
                    var created = target is null;
                    if (target is null)
                    {
                        if (sourceName is "SuperAdmin" or "Super Admin" or "Administrator" or "Manager" or "Sheriff")
                            throw new SsReject("MissingStandardRole");
                        var staff = (
                            await db
                                .Roles.Include(x => x.RolePermissions)
                                .Where(x => x.DeletedOn == null)
                                .ToListAsync(ct)
                        )
                            .Where(x => x.Name.Equals("Staff", StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        if (staff.Count != 1)
                            throw new SsReject("MissingStaffPermissionSnapshot");
                        target = session.Stamp(new Role { Name = name, Description = "" });
                        foreach (var permission in staff[0].RolePermissions)
                            target.RolePermissions.Add(
                                session.Stamp(new RolePermission { PermissionId = permission.PermissionId })
                            );
                        db.Roles.Add(target);
                        await db.SaveChangesAsync(ct);
                    }
                    await session.LinkAsync("Role", row, "Role", target.Id, hash, created, ct);
                },
                ct
            );

        var duplicates = rows["User"]
            .Where(x => x.Id != User.SystemUser.ToString())
            .GroupBy(x => User.NormalizeIdirName(x.Text("IdirName")) ?? "", StringComparer.Ordinal)
            .Where(x => x.Count() > 1)
            .SelectMany(x => x)
            .Select(x => x.Id)
            .ToHashSet();
        foreach (var row in rows["User"])
            await session.RowAsync(
                "User",
                row,
                async () =>
                {
                    if (row.Id == User.SystemUser.ToString())
                    {
                        await session.ResolveAsync("User", row.Id, ct);
                        session.Outcome("User", row.Id, "Excluded", "UnifiedSystemUser");
                        return;
                    }
                    if (duplicates.Contains(row.Id))
                        throw new SsReject("DuplicateNormalizedIdirName");
                    var idir = User.NormalizeIdirName(row.Text("IdirName")) ?? "";
                    var hash = SsWriteSession.Hash(
                        new
                        {
                            Home = row.Int("HomeLocationId"),
                            Enabled = row.Bool("IsEnabled"),
                            Gender = row.Int("Gender"),
                            LastLogin = row.Date("LastLogin"),
                        }
                    );
                    if (await session.ReplayAsync("User", row, hash, false, ct))
                        return;
                    var idirId = row.Guid("IdirId");
                    var keycloak = row.Guid("KeyCloakId");
                    if (
                        idirId is not null && rows["User"].Count(x => x.Guid("IdirId") == idirId) > 1
                        || keycloak is not null && rows["User"].Count(x => x.Guid("KeyCloakId") == keycloak) > 1
                    )
                        throw new SsReject("DuplicateSsoIdentity");
                    var targets = await db.Users.Where(x => x.IdirName == idir).ToListAsync(ct);
                    if (targets.Count > 1)
                        throw new SsReject("AmbiguousUser");
                    var target = targets.SingleOrDefault();
                    if (
                        target is not null
                        && (
                            idirId is not null && target.IdirId != idirId
                            || keycloak is not null && target.KeyCloakId != keycloak
                        )
                    )
                        throw new SsReject("ExistingUserSsoIdentityMismatch");
                    if (
                        await db.Users.AnyAsync(
                            x =>
                                (idirId != null && x.IdirId == idirId || keycloak != null && x.KeyCloakId == keycloak)
                                && (target == null || x.Id != target.Id),
                            ct
                        )
                    )
                        throw new SsReject("SsoIdentityCollision");
                    var created = target is null;
                    if (target is null)
                    {
                        var photo = row.Bytes("Photo");
                        var photoDate = row.Date("LastPhotoUpdate");
                        var rank = await ResolveRankAsync(row.Text("Rank"), rows, session, ct);
                        target = session.Stamp(
                            new User
                            {
                                Id = Guid.NewGuid(),
                                IdirName = idir,
                                IdirId = idirId,
                                KeyCloakId = keycloak,
                                IsEnabled = row.Bool("IsEnabled"),
                                FirstName = row.Text("FirstName") ?? "",
                                LastName = row.Text("LastName") ?? "",
                                Email = row.Text("Email") ?? "",
                                Gender =
                                    row.Int("Gender") is { } gender && Enum.IsDefined(typeof(Gender), gender)
                                        ? (Gender)gender
                                        : Gender.Other,
                                BadgeNumber = row.Text("BadgeNumber"),
                                LastLogin = row.Date("LastLogin"),
                                HomeLocationId = await session.ResolveIntAsync(
                                    "Location",
                                    row.Int("HomeLocationId"),
                                    ct
                                ),
                                Rank = rank?.Code,
                                EmployeeNumber = null,
                                PendingRegistration = false,
                                Photo = photo,
                                LastPhotoUpdate =
                                    photo is null ? null
                                    : photoDate is null || photoDate <= DateTimeOffset.MinValue ? session.Now
                                    : photoDate,
                            }
                        );
                        db.Users.Add(target);
                        await db.SaveChangesAsync(ct);
                    }
                    var missing =
                        string.IsNullOrWhiteSpace(row.Text("FirstName"))
                        || string.IsNullOrWhiteSpace(row.Text("LastName"))
                        || string.IsNullOrWhiteSpace(row.Text("Email"))
                        || string.IsNullOrWhiteSpace(row.Text("IdirName"));
                    await session.LinkAsync(
                        "User",
                        row,
                        "User",
                        target.Id,
                        hash,
                        created,
                        ct,
                        missing ? "MigratedWithDifference" : "Migrated",
                        missing ? "NullIdentityTextToEmpty" : null
                    );
                },
                ct
            );
        foreach (var row in rows["UserRole"])
            await session.RowAsync(
                "UserRole",
                row,
                async () =>
                {
                    var hash = SsWriteSession.Hash(
                        new
                        {
                            User = row.Guid("UserId"),
                            Role = row.Int("RoleId"),
                            Effective = row.Date("EffectiveDate"),
                            Expiry = row.Date("ExpiryDate"),
                        }
                    );
                    if (await session.ReplayAsync("UserRole", row, hash, false, ct))
                        return;
                    var target = session.Stamp(
                        new UserRole
                        {
                            UserId = Guid.Parse(await session.ResolveAsync("User", row.Guid("UserId")?.ToString(), ct)),
                            RoleId =
                                await session.ResolveIntAsync("Role", row.Int("RoleId"), ct)
                                ?? throw new SsReject("MissingRole"),
                            EffectiveDate = row.Date("EffectiveDate") ?? throw new SsReject("MissingEffectiveDate"),
                            ExpiryDate = row.Date("ExpiryDate"),
                            ExpiryReason = row.Text("ExpiryReason"),
                        }
                    );
                    db.UserRoles.Add(target);
                    await db.SaveChangesAsync(ct);
                    await session.LinkAsync("UserRole", row, "UserRole", target.Id, hash, true, ct);
                },
                ct
            );
        foreach (var row in rows["SheriffActingRank"])
            await session.RowAsync(
                "SheriffActingRank",
                row,
                async () =>
                {
                    if (row.Date("EndDate") < boundary)
                    {
                        session.Outcome("SheriffActingRank", row.Id, "Excluded", "BeforeHistoryBoundary");
                        return;
                    }
                    var dates = SsWriteSession.Dates(row);
                    if (dates.Start == dates.End)
                        throw new SsReject("EmptyInterval");
                    var timezone = SsWriteSession.Timezone(row, false);
                    var hash = SsWriteSession.Hash(
                        new
                        {
                            dates,
                            timezone,
                            User = row.Guid("SheriffId"),
                            Expiry = row.Date("ExpiryDate"),
                        }
                    );
                    if (await session.ReplayAsync("SheriffActingRank", row, hash, false, ct))
                        return;
                    var rank =
                        await ResolveRankAsync(row.Text("Rank"), rows, session, ct)
                        ?? throw new SsReject("MissingRank");
                    var target = session.Stamp(
                        new UserActingPosition
                        {
                            UserId = Guid.Parse(
                                await session.ResolveAsync("User", row.Guid("SheriffId")?.ToString(), ct)
                            ),
                            PositionTypeId = rank.Id,
                            StartAtUtc = dates.Start,
                            EndAtUtc = dates.End,
                            Timezone = timezone,
                            Comment = row.Text("Comment"),
                            ExpiryAtUtc = row.Date("ExpiryDate"),
                            ExpiryReason = row.Text("ExpiryReason"),
                        }
                    );
                    db.UserActingPositions.Add(target);
                    await db.SaveChangesAsync(ct);
                    await session.LinkAsync("SheriffActingRank", row, "UserActingPosition", target.Id, hash, true, ct);
                },
                ct
            );
        foreach (var row in rows["LookupSortOrder"])
            session.Outcome("LookupSortOrder", row.Id, "Excluded", "ApprovedOrderOmission");
        await leaveWriter.EntriesAsync(rows["SheriffLeave"], session, boundary, ct);
        // Current target removed UserAwayLocations on 2026-11-11. No invented user/event relationship.
        foreach (var row in rows["SheriffAwayLocation"])
            session.Outcome(
                "SheriffAwayLocation",
                row.Id,
                row.Date("EndDate") < boundary ? "Excluded" : "NeedsAction",
                row.Date("EndDate") < boundary ? "BeforeHistoryBoundary" : "TargetAwayLocationModelRemoved"
            );
    }

    private async Task<PositionType?> ResolveRankAsync(
        string? value,
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession session,
        CancellationToken ct
    )
    {
        if (value is null)
            return null;
        var positions = rows["LookupCode"].Where(x => x.Int("Type") == 7).ToList();
        var matches = positions.Where(x => x.Text("Code") == value).ToList();
        if (matches.Count == 0)
            matches = positions.Where(x => x.Text("Description") == value).ToList();
        if (matches.Count != 1)
            throw new SsReject("UnresolvedRank");
        var id = await session.ResolveIntAsync("PositionType", matches[0].Int("Id"), ct);
        return await db.PositionTypes.SingleAsync(x => x.Id == id, ct);
    }
}
