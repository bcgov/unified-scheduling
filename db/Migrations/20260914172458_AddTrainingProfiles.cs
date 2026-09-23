using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TrainingProfileId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TrainingProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EffectiveDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingProfiles_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TrainingProfiles_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TrainingMandatoryTrainingProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TrainingId = table.Column<int>(type: "integer", nullable: false),
                    TrainingProfileId = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingMandatoryTrainingProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingMandatoryTrainingProfiles_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TrainingMandatoryTrainingProfiles_TrainingProfiles_Training~",
                        column: x => x.TrainingProfileId,
                        principalTable: "TrainingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainingMandatoryTrainingProfiles_Trainings_TrainingId",
                        column: x => x.TrainingId,
                        principalTable: "Trainings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainingMandatoryTrainingProfiles_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TrainingProfileId",
                table: "Users",
                column: "TrainingProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingMandatoryTrainingProfiles_TrainingProfileId",
                table: "TrainingMandatoryTrainingProfiles",
                column: "TrainingProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingMandatoryTrainingProfiles_CreatedById",
                table: "TrainingMandatoryTrainingProfiles",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingMandatoryTrainingProfiles_TrainingId_TrainingProfileId",
                table: "TrainingMandatoryTrainingProfiles",
                columns: new[] { "TrainingId", "TrainingProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingMandatoryTrainingProfiles_UpdatedById",
                table: "TrainingMandatoryTrainingProfiles",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingProfiles_Code",
                table: "TrainingProfiles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingProfiles_CreatedById",
                table: "TrainingProfiles",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingProfiles_UpdatedById",
                table: "TrainingProfiles",
                column: "UpdatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_TrainingProfiles_TrainingProfileId",
                table: "Users",
                column: "TrainingProfileId",
                principalTable: "TrainingProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_TrainingProfiles_TrainingProfileId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "TrainingMandatoryTrainingProfiles");

            migrationBuilder.DropTable(
                name: "TrainingProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Users_TrainingProfileId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TrainingProfileId",
                table: "Users");
        }
    }
}
