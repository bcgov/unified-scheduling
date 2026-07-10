using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations;

/// <inheritdoc />
[DbContext(typeof(UnifiedDbContext))]
[Migration("20260709153000_AddAssignmentSubCategoryParentRelationship")]
public partial class AddAssignmentSubCategoryParentRelationship : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "AssignmentSubCategoryTypes"
            ADD COLUMN "AssignmentCategoryTypeId" integer NULL;

            UPDATE "AssignmentSubCategoryTypes" AS sub
            SET "AssignmentCategoryTypeId" = category."Id"
            FROM "AssignmentCategoryTypes" AS category
            WHERE sub."Code" IN ('PROVINCIAL', 'SUPREME')
              AND category."Code" = 'CourtRoom';

            UPDATE "AssignmentSubCategoryTypes" AS sub
            SET "AssignmentCategoryTypeId" = category."Id"
            FROM "AssignmentCategoryTypes" AS category
            WHERE sub."Code" IN ('IN_CUSTODY', 'OUT_OF_CUSTODY')
              AND category."Code" = 'EscortRun';

            UPDATE "AssignmentSubCategoryTypes" AS sub
            SET "AssignmentCategoryTypeId" = category."Id"
            FROM "AssignmentCategoryTypes" AS category
            WHERE sub."Code" = 'OTHER'
              AND category."Code" = 'OtherAssignment';

            UPDATE "AssignmentSubCategoryTypes" AS sub
            SET "AssignmentCategoryTypeId" = category."Id"
            FROM "AssignmentCategoryTypes" AS category
            WHERE sub."AssignmentCategoryTypeId" IS NULL
              AND category."Code" = 'OtherAssignment';

            ALTER TABLE "AssignmentSubCategoryTypes"
            ALTER COLUMN "AssignmentCategoryTypeId" SET NOT NULL;

            DROP INDEX IF EXISTS "IX_AssignmentSubCategoryTypes_Code";
            CREATE INDEX "IX_AssignmentSubCategoryTypes_AssignmentCategoryTypeId"
                ON "AssignmentSubCategoryTypes" ("AssignmentCategoryTypeId");
            CREATE UNIQUE INDEX "IX_AssignmentSubCategoryTypes_AssignmentCategoryTypeId_Code"
                ON "AssignmentSubCategoryTypes" ("AssignmentCategoryTypeId", "Code");

            ALTER TABLE "AssignmentSubCategoryTypes"
            ADD CONSTRAINT "FK_AssignmentSubCategoryTypes_AssignmentCategoryTypes_AssignmentCategoryTypeId"
            FOREIGN KEY ("AssignmentCategoryTypeId") REFERENCES "AssignmentCategoryTypes" ("Id") ON DELETE RESTRICT;
            """
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "AssignmentSubCategoryTypes"
            DROP CONSTRAINT IF EXISTS "FK_AssignmentSubCategoryTypes_AssignmentCategoryTypes_AssignmentCategoryTypeId";

            DROP INDEX IF EXISTS "IX_AssignmentSubCategoryTypes_AssignmentCategoryTypeId_Code";
            DROP INDEX IF EXISTS "IX_AssignmentSubCategoryTypes_AssignmentCategoryTypeId";
            CREATE UNIQUE INDEX "IX_AssignmentSubCategoryTypes_Code" ON "AssignmentSubCategoryTypes" ("Code");

            ALTER TABLE "AssignmentSubCategoryTypes"
            DROP COLUMN IF EXISTS "AssignmentCategoryTypeId";
            """
        );
    }
}