using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class SplitAssignmentsCreateAssignPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "Permissions" ("Id", "Group", "Description")
                VALUES
                    ('AssignmentsCreate', 'Scheduling', 'Create assignments'),
                    ('AssignmentsAssign', 'Scheduling', 'Assign shifts to assignments')
                ON CONFLICT ("Id") DO UPDATE
                SET "Group" = EXCLUDED."Group",
                    "Description" = EXCLUDED."Description";

                INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
                SELECT DISTINCT source."RoleId", target."PermissionId"
                FROM "RolePermissions" source
                CROSS JOIN (VALUES ('AssignmentsCreate'), ('AssignmentsAssign')) AS target("PermissionId")
                WHERE source."PermissionId" = 'AssignmentsCreateAndAssign'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "RolePermissions" existing
                      WHERE existing."RoleId" = source."RoleId"
                        AND existing."PermissionId" = target."PermissionId"
                  );

                INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
                SELECT DISTINCT source."RoleId", 'AssignmentsAssign'
                FROM "RolePermissions" source
                WHERE source."PermissionId" = 'ShiftAssignment'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "RolePermissions" existing
                      WHERE existing."RoleId" = source."RoleId"
                        AND existing."PermissionId" = 'AssignmentsAssign'
                  );

                DELETE FROM "RolePermissions"
                WHERE "PermissionId" IN ('AssignmentsCreateAndAssign', 'ShiftAssignment');

                DELETE FROM "Permissions"
                WHERE "Id" IN ('AssignmentsCreateAndAssign', 'ShiftAssignment');
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "Permissions" ("Id", "Group", "Description")
                VALUES
                    ('AssignmentsCreateAndAssign', 'Scheduling', 'Create assignments and assign shifts to them'),
                    ('ShiftAssignment', 'Scheduling', 'Link shifts to assignments')
                ON CONFLICT ("Id") DO UPDATE
                SET "Group" = EXCLUDED."Group",
                    "Description" = EXCLUDED."Description";

                INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
                SELECT DISTINCT source."RoleId", 'AssignmentsCreateAndAssign'
                FROM "RolePermissions" source
                WHERE source."PermissionId" IN ('AssignmentsCreate', 'AssignmentsAssign')
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "RolePermissions" existing
                      WHERE existing."RoleId" = source."RoleId"
                        AND existing."PermissionId" = 'AssignmentsCreateAndAssign'
                  );

                INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
                SELECT DISTINCT source."RoleId", 'ShiftAssignment'
                FROM "RolePermissions" source
                WHERE source."PermissionId" = 'AssignmentsAssign'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "RolePermissions" existing
                      WHERE existing."RoleId" = source."RoleId"
                        AND existing."PermissionId" = 'ShiftAssignment'
                  );

                DELETE FROM "RolePermissions"
                WHERE "PermissionId" IN ('AssignmentsCreate', 'AssignmentsAssign');

                DELETE FROM "Permissions"
                WHERE "Id" IN ('AssignmentsCreate', 'AssignmentsAssign');
                """
            );
        }
    }
}
