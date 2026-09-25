using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingProfileTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TrainingProfileId",
                table: "Users",
                type: "integer",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "TrainingProfileTypes",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:IdentitySequenceOptions",
                            "'200', '1', '', '', 'False', '1'"
                        )
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    Code = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    Name = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "now()"
                    ),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    EffectiveDate = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    ExpiryDate = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingProfileTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingProfileTypes_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_TrainingProfileTypes_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "TrainingMandatoryTrainingProfiles",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    TrainingId = table.Column<int>(type: "integer", nullable: false),
                    TrainingProfileTypeId = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "now()"
                    ),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingMandatoryTrainingProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingMandatoryTrainingProfiles_TrainingProfileTypes_Trai~",
                        column: x => x.TrainingProfileTypeId,
                        principalTable: "TrainingProfileTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_TrainingMandatoryTrainingProfiles_Trainings_TrainingId",
                        column: x => x.TrainingId,
                        principalTable: "Trainings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_TrainingMandatoryTrainingProfiles_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_TrainingMandatoryTrainingProfiles_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Users_TrainingProfileId",
                table: "Users",
                column: "TrainingProfileId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TrainingMandatoryTrainingProfiles_CreatedById",
                table: "TrainingMandatoryTrainingProfiles",
                column: "CreatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TrainingMandatoryTrainingProfiles_TrainingId_TrainingProfil~",
                table: "TrainingMandatoryTrainingProfiles",
                columns: new[] { "TrainingId", "TrainingProfileTypeId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_TrainingMandatoryTrainingProfiles_TrainingProfileTypeId",
                table: "TrainingMandatoryTrainingProfiles",
                column: "TrainingProfileTypeId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TrainingMandatoryTrainingProfiles_UpdatedById",
                table: "TrainingMandatoryTrainingProfiles",
                column: "UpdatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TrainingProfileTypes_Code",
                table: "TrainingProfileTypes",
                column: "Code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_TrainingProfileTypes_CreatedById",
                table: "TrainingProfileTypes",
                column: "CreatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TrainingProfileTypes_UpdatedById",
                table: "TrainingProfileTypes",
                column: "UpdatedById"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Users_TrainingProfileTypes_TrainingProfileId",
                table: "Users",
                column: "TrainingProfileId",
                principalTable: "TrainingProfileTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_TrainingProfileTypes_TrainingProfileId",
                table: "Users"
            );

            migrationBuilder.DropTable(name: "TrainingMandatoryTrainingProfiles");

            migrationBuilder.DropTable(name: "TrainingProfileTypes");

            migrationBuilder.DropIndex(name: "IX_Users_TrainingProfileId", table: "Users");

            migrationBuilder.DropColumn(name: "TrainingProfileId", table: "Users");
        }
    }
}
