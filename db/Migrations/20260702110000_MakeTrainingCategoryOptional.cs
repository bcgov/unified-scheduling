using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    [DbContext(typeof(UnifiedDbContext))]
    [Migration("20260702110000_MakeTrainingCategoryOptional")]
    /// <inheritdoc />
    public partial class MakeTrainingCategoryOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trainings_TrainingCategories_TrainingCategoryId",
                table: "Trainings"
            );

            migrationBuilder.AlterColumn<int>(
                name: "TrainingCategoryId",
                table: "Trainings",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Trainings_TrainingCategories_TrainingCategoryId",
                table: "Trainings",
                column: "TrainingCategoryId",
                principalTable: "TrainingCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trainings_TrainingCategories_TrainingCategoryId",
                table: "Trainings"
            );

            migrationBuilder.AlterColumn<int>(
                name: "TrainingCategoryId",
                table: "Trainings",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Trainings_TrainingCategories_TrainingCategoryId",
                table: "Trainings",
                column: "TrainingCategoryId",
                principalTable: "TrainingCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }
    }
}
