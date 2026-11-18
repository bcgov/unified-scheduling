using Microsoft.EntityFrameworkCore;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
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
                        if (sourceName is "SuperAdmin" or "Super Admin" or "Manager" or "Sheriff")
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
        var sourceUsers = rows["User"];
        var duplicateIdentityGroups = DuplicateIdentityGroups(sourceUsers);
        var effectiveIdentities = new Dictionary<string, SsRow>(StringComparer.Ordinal);
        var duplicateIdentityRejections = new Dictionary<string, string>(StringComparer.Ordinal);
        var staleBindingLosers = new HashSet<string>(StringComparer.Ordinal);
        var usersWithHistory = rows.TryGetValue("__UsersWithHistory", out var historyRows)
            ? historyRows.Select(x => x.Id).ToHashSet(StringComparer.Ordinal)
            : null;
        foreach (var group in duplicateIdentityGroups)
        {
            if (group.Any(x => x.Id == User.SystemUser.ToString()))
            {
                foreach (var member in group)
                    duplicateIdentityRejections[member.Id] = "DuplicateSsoIdentity";
                continue;
            }

            if (group.Any(x => duplicates.Contains(x.Id)))
            {
                foreach (var member in group)
                    duplicateIdentityRejections[member.Id] = "DuplicateNormalizedIdirName";
                continue;
            }

            var survivors = group
                .Where(x => x.Bool("IsEnabled") && x.Guid("IdirId") is not null)
                .ToList();
            if (survivors.Count != 1)
            {
                foreach (var member in group)
                    duplicateIdentityRejections[member.Id] = "DuplicateSsoIdentity";
                continue;
            }

            var survivor = survivors[0];
            var losers = group.Where(x => !ReferenceEquals(x, survivor)).ToList();
            if (
                losers.Any(x => x.Bool("IsEnabled") || x.Guid("IdirId") is not null)
                || rows["User"].Count(x => x.Guid("IdirId") == survivor.Guid("IdirId")) != 1
            )
            {
                foreach (var member in group)
                    duplicateIdentityRejections[member.Id] = "DuplicateSsoIdentity";
                continue;
            }

            if (usersWithHistory is null)
            {
                foreach (var member in group)
                    duplicateIdentityRejections[member.Id] = "StaleSsoBindingHistoryUnavailable";
                continue;
            }

            if (losers.Any(x => usersWithHistory.Contains(x.Id)))
            {
                foreach (var member in group)
                    duplicateIdentityRejections[member.Id] = "StaleSsoBindingHasLiveHistory";
                continue;
            }

            foreach (var member in losers)
            {
                effectiveIdentities[member.Id] = member.WithUserIdentities(null, null);
                staleBindingLosers.Add(member.Id);
            }
        }
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
                    if (duplicateIdentityRejections.TryGetValue(row.Id, out var identityRejection))
                        throw new SsReject(identityRejection);
                    var idir = User.NormalizeIdirName(row.Text("IdirName")) ?? "";
                    var effectiveRow = effectiveIdentities.GetValueOrDefault(row.Id) ?? row;
                    var idirId = effectiveRow.Guid("IdirId");
                    var keycloak = effectiveRow.Guid("KeyCloakId");
                    var outcomeReason = staleBindingLosers.Contains(row.Id) ? "StaleSsoBindingCleared" : null;
                    var hash = SsWriteSession.Hash(
                        new
                        {
                            Home = row.Int("HomeLocationId"),
                            Enabled = row.Bool("IsEnabled"),
                            Gender = row.Int("Gender"),
                            LastLogin = row.Date("LastLogin"),
                            IdirId = idirId,
                            KeyCloakId = keycloak,
                            IdentityDifference = outcomeReason,
                        }
                    );
                    if (
                        await session.ReplayAsync(
                            "User",
                            effectiveRow,
                            hash,
                            false,
                            ct,
                            async () =>
                            {
                                var id = Guid.Parse(await session.ResolveAsync("User", row.Id, ct));
                                if (
                                    await db.Users.AnyAsync(
                                        x =>
                                            x.Id != id
                                            && (
                                                x.IdirName == idir
                                                || idirId != null && x.IdirId == idirId
                                                || keycloak != null && x.KeyCloakId == keycloak
                                            ),
                                        ct
                                    )
                                )
                                    throw new SsReject("UserIdentityCollision");
                                if (row.Int("Gender") is { } invalid && !Enum.IsDefined(typeof(Gender), invalid))
                                    throw new SsReject("InvalidGender");
                                var owned = await db.Users.SingleAsync(x => x.Id == id, ct);
                                var rank = await ResolveRankAsync(row.Text("Rank"), rows, session, ct);
                                owned.IdirName = idir;
                                owned.IdirId = idirId;
                                owned.KeyCloakId = keycloak;
                                owned.IsEnabled = row.Bool("IsEnabled");
                                owned.FirstName = row.Text("FirstName") ?? "";
                                owned.LastName = row.Text("LastName") ?? "";
                                owned.Email = row.Text("Email") ?? "";
                                owned.Gender = row.Int("Gender") is { } gender ? (Gender)gender : Gender.Other;
                                owned.BadgeNumber = row.Text("BadgeNumber");
                                owned.LastLogin = row.Date("LastLogin");
                                owned.HomeLocationId = await session.ResolveIntAsync(
                                    "Location",
                                    row.Int("HomeLocationId"),
                                    ct
                                );
                                owned.Rank = rank?.Code;
                                owned.EmployeeNumber = null;
                                owned.PendingRegistration = false;
                                owned.Photo = row.Bytes("Photo");
                                var photoDate = row.Date("LastPhotoUpdate");
                                owned.LastPhotoUpdate =
                                    owned.Photo is null ? null
                                    : photoDate is null || photoDate <= DateTimeOffset.MinValue ? session.Now
                                    : photoDate;
                                owned.UpdatedOn = session.Now;
                                // TrainingProfileId belongs exclusively to Training, never MyTeam.
                            }
                        )
                    )
                    {
                        if (outcomeReason is not null)
                            session.Outcome("User", row.Id, "MigratedWithDifference", outcomeReason);
                        return;
                    }
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
                        if (row.Int("Gender") is { } suppliedGender && !Enum.IsDefined(typeof(Gender), suppliedGender))
                            throw new SsReject("InvalidGender");
                        var rank = await ResolveRankAsync(row.Text("Rank"), rows, session, ct);
                        target = session.Stamp(
                            new User
                            {
                                Id = session.NewUserId(row.Id),
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
                        effectiveRow,
                        "User",
                        target.Id,
                        hash,
                        created,
                        ct,
                        missing || outcomeReason is not null ? "MigratedWithDifference" : "Migrated",
                        outcomeReason ?? (missing ? "NullIdentityTextToEmpty" : null)
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
                    if (
                        await session.ReplayAsync(
                            "UserRole",
                            row,
                            hash,
                            false,
                            ct,
                            async () =>
                            {
                                var id = int.Parse(await session.ResolveAsync("UserRole", row.Id, ct));
                                var owned = await db.UserRoles.SingleAsync(x => x.Id == id, ct);
                                owned.UserId = Guid.Parse(
                                    await session.ResolveAsync("User", row.Guid("UserId")?.ToString(), ct)
                                );
                                owned.RoleId =
                                    await session.ResolveIntAsync("Role", row.Int("RoleId"), ct)
                                    ?? throw new SsReject("MissingRole");
                                owned.EffectiveDate =
                                    row.Date("EffectiveDate") ?? throw new SsReject("MissingEffectiveDate");
                                owned.ExpiryDate = row.Date("ExpiryDate");
                                owned.ExpiryReason = row.Text("ExpiryReason");
                                owned.UpdatedOn = session.Now;
                            }
                        )
                    )
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
                    if (
                        await session.ReplayAsync(
                            "SheriffActingRank",
                            row,
                            hash,
                            false,
                            ct,
                            async () =>
                            {
                                var id = int.Parse(await session.ResolveAsync("SheriffActingRank", row.Id, ct));
                                var owned = await db.UserActingPositions.SingleAsync(x => x.Id == id, ct);
                                var position =
                                    await ResolveRankAsync(row.Text("Rank"), rows, session, ct)
                                    ?? throw new SsReject("MissingRank");
                                owned.UserId = Guid.Parse(
                                    await session.ResolveAsync("User", row.Guid("SheriffId")?.ToString(), ct)
                                );
                                owned.PositionTypeId = position.Id;
                                owned.StartAtUtc = dates.Start;
                                owned.EndAtUtc = dates.End;
                                owned.Timezone = timezone;
                                owned.Comment = row.Text("Comment");
                                owned.ExpiryAtUtc = row.Date("ExpiryDate");
                                owned.ExpiryReason = row.Text("ExpiryReason");
                                owned.UpdatedOn = session.Now;
                            }
                        )
                    )
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
        foreach (var row in rows["SheriffAwayLocation"])
            await session.RowAsync(
                "SheriffAwayLocation",
                row,
                async () =>
                {
                    if (row.Date("EndDate") < boundary)
                    {
                        session.Outcome("SheriffAwayLocation", row.Id, "Excluded", "BeforeHistoryBoundary");
                        return;
                    }
                    var dates = SsWriteSession.Dates(row);
                    if (dates.Start == dates.End)
                        throw new SsReject("EmptyInterval");
                    var zone = SsWriteSession.Timezone(row, false);
                    var userId = Guid.Parse(await session.ResolveAsync("User", row.Guid("SheriffId")?.ToString(), ct));
                    var destination =
                        await session.ResolveIntAsync("Location", row.Int("LocationId"), ct)
                        ?? throw new SsReject("MissingAwayLocation");
                    var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
                    var location = await db.Locations.SingleAsync(x => x.Id == destination, ct);
                    var fallback = zone;
                    if (fallback is null && user.HomeLocationId is { } home)
                        fallback = await db.Locations.Where(x => x.Id == home).Select(x => x.Timezone).SingleAsync(ct);
                    var hash = SsWriteSession.Hash(
                        new
                        {
                            userId,
                            destination,
                            Home = user.HomeLocationId,
                            Title = $"Away - {location.Name}",
                            dates,
                            zone,
                            Expiry = row.Date("ExpiryDate"),
                        }
                    );
                    if (
                        await session.ReplayAsync(
                            "SheriffAwayLocation",
                            row,
                            hash,
                            false,
                            ct,
                            async () =>
                            {
                                var id = int.Parse(await session.ResolveAsync("SheriffAwayLocation", row.Id, ct));
                                var owned = await db
                                    .LoanOutEntries.Include(x => x.Event)
                                    .Include(x => x.Users)
                                    .SingleAsync(x => x.Id == id, ct);
                                if (owned.Users.Count != 1)
                                    throw new SsReject("AmbiguousOwnedAwayUsers");
                                owned.Users.Single().UserId = userId;
                                owned.LoanOutLocationId = destination;
                                owned.UpdatedOn = session.Now;
                                var e = owned.Event!;
                                e.Title = $"Away - {location.Name}";
                                e.Description = row.Text("Comment");
                                e.StartAtUtc = dates.Start;
                                e.EndAtUtc = dates.End;
                                e.TimeZoneId = zone;
                                e.LocationId = user.HomeLocationId;
                                e.StatusTypeCode = SsLeaveWriter.EventStatus(
                                    dates.Start,
                                    fallback,
                                    row.Date("ExpiryDate")
                                );
                                e.CancelledAt = row.Date("ExpiryDate");
                                e.CancellationReason = row.Text("ExpiryReason");
                                e.UpdatedOn = session.Now;
                            }
                        )
                    )
                        return;
                    // Current Unified consumes away intervals through LoanOutEntry, not the removed
                    // UserAwayLocations table. Do not call CreateLoanOutEntryAsync: it forces all-day
                    // and transfers/regenerates shifts which must instead come from SS unchanged.
                    var entry = session.Stamp(
                        new LoanOutEntry
                        {
                            LoanOutLocationId = destination,
                            Event = session.Stamp(
                                new Event
                                {
                                    Title = $"Away - {location.Name}",
                                    Description = row.Text("Comment"),
                                    StartAtUtc = dates.Start,
                                    EndAtUtc = dates.End,
                                    TimeZoneId = zone,
                                    AllDay = false,
                                    EventTypeCode = "loanOut",
                                    SourceModule = "loanOut",
                                    LocationId = user.HomeLocationId,
                                    StatusTypeCode = SsLeaveWriter.EventStatus(
                                        dates.Start,
                                        fallback,
                                        row.Date("ExpiryDate")
                                    ),
                                    CancelledAt = row.Date("ExpiryDate"),
                                    CancellationReason = row.Text("ExpiryReason"),
                                }
                            ),
                        }
                    );
                    entry.Users.Add(session.Stamp(new LoanOutEntryUser { UserId = userId }));
                    db.LoanOutEntries.Add(entry);
                    await db.SaveChangesAsync(ct);
                    await session.LinkAsync(
                        "SheriffAwayLocation",
                        row,
                        "LoanOutEntry",
                        entry.Id,
                        hash,
                        true,
                        ct,
                        "MigratedWithDifference",
                        "AwayToLoanOutDayAvailability"
                    );
                },
                ct
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

    private static List<List<SsRow>> DuplicateIdentityGroups(List<SsRow> users)
    {
        var parents = Enumerable.Range(0, users.Count).ToArray();

        int Find(int index)
        {
            while (parents[index] != index)
            {
                parents[index] = parents[parents[index]];
                index = parents[index];
            }
            return index;
        }

        void Union(int left, int right)
        {
            var leftRoot = Find(left);
            var rightRoot = Find(right);
            if (leftRoot != rightRoot)
                parents[rightRoot] = leftRoot;
        }

        var firstByIdentity = new Dictionary<(string Kind, Guid Value), int>();
        for (var index = 0; index < users.Count; index++)
        {
            var identities = new List<(string Kind, Guid Value)>();
            if (users[index].Guid("IdirId") is { } idir)
                identities.Add(("IdirId", idir));
            if (users[index].Guid("KeyCloakId") is { } keycloak)
                identities.Add(("KeyCloakId", keycloak));
            foreach (var key in identities)
            {
                if (firstByIdentity.TryGetValue(key, out var first))
                    Union(first, index);
                else
                    firstByIdentity.Add(key, index);
            }
        }

        return Enumerable.Range(0, users.Count)
            .GroupBy(Find)
            .Where(x => x.Count() > 1)
            .Select(x => x.Select(index => users[index]).ToList())
            .ToList();
    }

}
