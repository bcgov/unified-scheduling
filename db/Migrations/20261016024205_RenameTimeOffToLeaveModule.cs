using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class RenameTimeOffToLeaveModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffEntries_Events_EventId",
                table: "TimeOffEntries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffEntries_LeaveTypes_LeaveTypeId",
                table: "TimeOffEntries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffEntries_TimeOffSeries_TimeOffSeriesId",
                table: "TimeOffEntries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffEntries_Users_CreatedById",
                table: "TimeOffEntries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffEntries_Users_UpdatedById",
                table: "TimeOffEntries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffEntryUsers_TimeOffEntries_TimeOffEntryId",
                table: "TimeOffEntryUsers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffEntryUsers_Users_CreatedById",
                table: "TimeOffEntryUsers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffEntryUsers_Users_UpdatedById",
                table: "TimeOffEntryUsers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffEntryUsers_Users_UserId",
                table: "TimeOffEntryUsers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffSeries_EventSeries_EventSeriesId",
                table: "TimeOffSeries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffSeries_LeaveTypes_LeaveTypeId",
                table: "TimeOffSeries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffSeries_Users_CreatedById",
                table: "TimeOffSeries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffSeries_Users_UpdatedById",
                table: "TimeOffSeries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffSeriesUsers_TimeOffSeries_TimeOffSeriesId",
                table: "TimeOffSeriesUsers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffSeriesUsers_Users_CreatedById",
                table: "TimeOffSeriesUsers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffSeriesUsers_Users_UpdatedById",
                table: "TimeOffSeriesUsers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffSeriesUsers_Users_UserId",
                table: "TimeOffSeriesUsers"
            );

            migrationBuilder.DropPrimaryKey(
                name: "PK_TimeOffSeriesUsers",
                table: "TimeOffSeriesUsers"
            );

            migrationBuilder.DropPrimaryKey(name: "PK_TimeOffSeries", table: "TimeOffSeries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TimeOffEntryUsers",
                table: "TimeOffEntryUsers"
            );

            migrationBuilder.DropPrimaryKey(name: "PK_TimeOffEntries", table: "TimeOffEntries");

            migrationBuilder.RenameTable(name: "TimeOffSeriesUsers", newName: "LeaveSeriesUsers");

            migrationBuilder.RenameTable(name: "TimeOffSeries", newName: "LeaveSeries");

            migrationBuilder.RenameTable(name: "TimeOffEntryUsers", newName: "LeaveEntryUsers");

            migrationBuilder.RenameTable(name: "TimeOffEntries", newName: "LeaveEntries");

            migrationBuilder.RenameColumn(
                name: "TimeOffSeriesId",
                table: "LeaveSeriesUsers",
                newName: "LeaveSeriesId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffSeriesUsers_UserId",
                table: "LeaveSeriesUsers",
                newName: "IX_LeaveSeriesUsers_UserId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffSeriesUsers_UpdatedById",
                table: "LeaveSeriesUsers",
                newName: "IX_LeaveSeriesUsers_UpdatedById"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffSeriesUsers_TimeOffSeriesId_UserId",
                table: "LeaveSeriesUsers",
                newName: "IX_LeaveSeriesUsers_LeaveSeriesId_UserId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffSeriesUsers_TimeOffSeriesId",
                table: "LeaveSeriesUsers",
                newName: "IX_LeaveSeriesUsers_LeaveSeriesId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffSeriesUsers_CreatedById",
                table: "LeaveSeriesUsers",
                newName: "IX_LeaveSeriesUsers_CreatedById"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffSeries_UpdatedById",
                table: "LeaveSeries",
                newName: "IX_LeaveSeries_UpdatedById"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffSeries_LeaveTypeId",
                table: "LeaveSeries",
                newName: "IX_LeaveSeries_LeaveTypeId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffSeries_EventSeriesId",
                table: "LeaveSeries",
                newName: "IX_LeaveSeries_EventSeriesId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffSeries_CreatedById",
                table: "LeaveSeries",
                newName: "IX_LeaveSeries_CreatedById"
            );

            migrationBuilder.RenameColumn(
                name: "TimeOffEntryId",
                table: "LeaveEntryUsers",
                newName: "LeaveEntryId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffEntryUsers_UserId",
                table: "LeaveEntryUsers",
                newName: "IX_LeaveEntryUsers_UserId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffEntryUsers_UpdatedById",
                table: "LeaveEntryUsers",
                newName: "IX_LeaveEntryUsers_UpdatedById"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffEntryUsers_TimeOffEntryId_UserId",
                table: "LeaveEntryUsers",
                newName: "IX_LeaveEntryUsers_LeaveEntryId_UserId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffEntryUsers_TimeOffEntryId",
                table: "LeaveEntryUsers",
                newName: "IX_LeaveEntryUsers_LeaveEntryId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffEntryUsers_CreatedById",
                table: "LeaveEntryUsers",
                newName: "IX_LeaveEntryUsers_CreatedById"
            );

            migrationBuilder.RenameColumn(
                name: "TimeOffSeriesId",
                table: "LeaveEntries",
                newName: "LeaveSeriesId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffEntries_UpdatedById",
                table: "LeaveEntries",
                newName: "IX_LeaveEntries_UpdatedById"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffEntries_TimeOffSeriesId",
                table: "LeaveEntries",
                newName: "IX_LeaveEntries_LeaveSeriesId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffEntries_LeaveTypeId",
                table: "LeaveEntries",
                newName: "IX_LeaveEntries_LeaveTypeId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffEntries_EventId",
                table: "LeaveEntries",
                newName: "IX_LeaveEntries_EventId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_TimeOffEntries_CreatedById",
                table: "LeaveEntries",
                newName: "IX_LeaveEntries_CreatedById"
            );

            migrationBuilder.AddPrimaryKey(
                name: "PK_LeaveSeriesUsers",
                table: "LeaveSeriesUsers",
                column: "Id"
            );

            migrationBuilder.AddPrimaryKey(
                name: "PK_LeaveSeries",
                table: "LeaveSeries",
                column: "Id"
            );

            migrationBuilder.AddPrimaryKey(
                name: "PK_LeaveEntryUsers",
                table: "LeaveEntryUsers",
                column: "Id"
            );

            migrationBuilder.AddPrimaryKey(
                name: "PK_LeaveEntries",
                table: "LeaveEntries",
                column: "Id"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveEntries_Events_EventId",
                table: "LeaveEntries",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveEntries_LeaveSeries_LeaveSeriesId",
                table: "LeaveEntries",
                column: "LeaveSeriesId",
                principalTable: "LeaveSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveEntries_LeaveTypes_LeaveTypeId",
                table: "LeaveEntries",
                column: "LeaveTypeId",
                principalTable: "LeaveTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveEntries_Users_CreatedById",
                table: "LeaveEntries",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveEntries_Users_UpdatedById",
                table: "LeaveEntries",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveEntryUsers_LeaveEntries_LeaveEntryId",
                table: "LeaveEntryUsers",
                column: "LeaveEntryId",
                principalTable: "LeaveEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveEntryUsers_Users_CreatedById",
                table: "LeaveEntryUsers",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveEntryUsers_Users_UpdatedById",
                table: "LeaveEntryUsers",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveEntryUsers_Users_UserId",
                table: "LeaveEntryUsers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveSeries_EventSeries_EventSeriesId",
                table: "LeaveSeries",
                column: "EventSeriesId",
                principalTable: "EventSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveSeries_LeaveTypes_LeaveTypeId",
                table: "LeaveSeries",
                column: "LeaveTypeId",
                principalTable: "LeaveTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveSeries_Users_CreatedById",
                table: "LeaveSeries",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveSeries_Users_UpdatedById",
                table: "LeaveSeries",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveSeriesUsers_LeaveSeries_LeaveSeriesId",
                table: "LeaveSeriesUsers",
                column: "LeaveSeriesId",
                principalTable: "LeaveSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveSeriesUsers_Users_CreatedById",
                table: "LeaveSeriesUsers",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveSeriesUsers_Users_UpdatedById",
                table: "LeaveSeriesUsers",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveSeriesUsers_Users_UserId",
                table: "LeaveSeriesUsers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            // Identity-owned sequences are not renamed automatically when their table is renamed.
            migrationBuilder.Sql(
                "ALTER SEQUENCE \"TimeOffEntries_Id_seq\" RENAME TO \"LeaveEntries_Id_seq\";"
            );
            migrationBuilder.Sql(
                "ALTER SEQUENCE \"TimeOffEntryUsers_Id_seq\" RENAME TO \"LeaveEntryUsers_Id_seq\";"
            );
            migrationBuilder.Sql(
                "ALTER SEQUENCE \"TimeOffSeries_Id_seq\" RENAME TO \"LeaveSeries_Id_seq\";"
            );
            migrationBuilder.Sql(
                "ALTER SEQUENCE \"TimeOffSeriesUsers_Id_seq\" RENAME TO \"LeaveSeriesUsers_Id_seq\";"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveEntries_Events_EventId",
                table: "LeaveEntries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveEntries_LeaveSeries_LeaveSeriesId",
                table: "LeaveEntries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveEntries_LeaveTypes_LeaveTypeId",
                table: "LeaveEntries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveEntries_Users_CreatedById",
                table: "LeaveEntries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveEntries_Users_UpdatedById",
                table: "LeaveEntries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveEntryUsers_LeaveEntries_LeaveEntryId",
                table: "LeaveEntryUsers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveEntryUsers_Users_CreatedById",
                table: "LeaveEntryUsers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveEntryUsers_Users_UpdatedById",
                table: "LeaveEntryUsers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveEntryUsers_Users_UserId",
                table: "LeaveEntryUsers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveSeries_EventSeries_EventSeriesId",
                table: "LeaveSeries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveSeries_LeaveTypes_LeaveTypeId",
                table: "LeaveSeries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveSeries_Users_CreatedById",
                table: "LeaveSeries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveSeries_Users_UpdatedById",
                table: "LeaveSeries"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveSeriesUsers_LeaveSeries_LeaveSeriesId",
                table: "LeaveSeriesUsers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveSeriesUsers_Users_CreatedById",
                table: "LeaveSeriesUsers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveSeriesUsers_Users_UpdatedById",
                table: "LeaveSeriesUsers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveSeriesUsers_Users_UserId",
                table: "LeaveSeriesUsers"
            );

            migrationBuilder.DropPrimaryKey(name: "PK_LeaveSeriesUsers", table: "LeaveSeriesUsers");

            migrationBuilder.DropPrimaryKey(name: "PK_LeaveSeries", table: "LeaveSeries");

            migrationBuilder.DropPrimaryKey(name: "PK_LeaveEntryUsers", table: "LeaveEntryUsers");

            migrationBuilder.DropPrimaryKey(name: "PK_LeaveEntries", table: "LeaveEntries");

            migrationBuilder.RenameTable(name: "LeaveSeriesUsers", newName: "TimeOffSeriesUsers");

            migrationBuilder.RenameTable(name: "LeaveSeries", newName: "TimeOffSeries");

            migrationBuilder.RenameTable(name: "LeaveEntryUsers", newName: "TimeOffEntryUsers");

            migrationBuilder.RenameTable(name: "LeaveEntries", newName: "TimeOffEntries");

            migrationBuilder.RenameColumn(
                name: "LeaveSeriesId",
                table: "TimeOffSeriesUsers",
                newName: "TimeOffSeriesId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveSeriesUsers_UserId",
                table: "TimeOffSeriesUsers",
                newName: "IX_TimeOffSeriesUsers_UserId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveSeriesUsers_UpdatedById",
                table: "TimeOffSeriesUsers",
                newName: "IX_TimeOffSeriesUsers_UpdatedById"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveSeriesUsers_LeaveSeriesId_UserId",
                table: "TimeOffSeriesUsers",
                newName: "IX_TimeOffSeriesUsers_TimeOffSeriesId_UserId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveSeriesUsers_LeaveSeriesId",
                table: "TimeOffSeriesUsers",
                newName: "IX_TimeOffSeriesUsers_TimeOffSeriesId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveSeriesUsers_CreatedById",
                table: "TimeOffSeriesUsers",
                newName: "IX_TimeOffSeriesUsers_CreatedById"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveSeries_UpdatedById",
                table: "TimeOffSeries",
                newName: "IX_TimeOffSeries_UpdatedById"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveSeries_LeaveTypeId",
                table: "TimeOffSeries",
                newName: "IX_TimeOffSeries_LeaveTypeId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveSeries_EventSeriesId",
                table: "TimeOffSeries",
                newName: "IX_TimeOffSeries_EventSeriesId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveSeries_CreatedById",
                table: "TimeOffSeries",
                newName: "IX_TimeOffSeries_CreatedById"
            );

            migrationBuilder.RenameColumn(
                name: "LeaveEntryId",
                table: "TimeOffEntryUsers",
                newName: "TimeOffEntryId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveEntryUsers_UserId",
                table: "TimeOffEntryUsers",
                newName: "IX_TimeOffEntryUsers_UserId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveEntryUsers_UpdatedById",
                table: "TimeOffEntryUsers",
                newName: "IX_TimeOffEntryUsers_UpdatedById"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveEntryUsers_LeaveEntryId_UserId",
                table: "TimeOffEntryUsers",
                newName: "IX_TimeOffEntryUsers_TimeOffEntryId_UserId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveEntryUsers_LeaveEntryId",
                table: "TimeOffEntryUsers",
                newName: "IX_TimeOffEntryUsers_TimeOffEntryId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveEntryUsers_CreatedById",
                table: "TimeOffEntryUsers",
                newName: "IX_TimeOffEntryUsers_CreatedById"
            );

            migrationBuilder.RenameColumn(
                name: "LeaveSeriesId",
                table: "TimeOffEntries",
                newName: "TimeOffSeriesId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveEntries_UpdatedById",
                table: "TimeOffEntries",
                newName: "IX_TimeOffEntries_UpdatedById"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveEntries_LeaveTypeId",
                table: "TimeOffEntries",
                newName: "IX_TimeOffEntries_LeaveTypeId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveEntries_LeaveSeriesId",
                table: "TimeOffEntries",
                newName: "IX_TimeOffEntries_TimeOffSeriesId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveEntries_EventId",
                table: "TimeOffEntries",
                newName: "IX_TimeOffEntries_EventId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_LeaveEntries_CreatedById",
                table: "TimeOffEntries",
                newName: "IX_TimeOffEntries_CreatedById"
            );

            migrationBuilder.AddPrimaryKey(
                name: "PK_TimeOffSeriesUsers",
                table: "TimeOffSeriesUsers",
                column: "Id"
            );

            migrationBuilder.AddPrimaryKey(
                name: "PK_TimeOffSeries",
                table: "TimeOffSeries",
                column: "Id"
            );

            migrationBuilder.AddPrimaryKey(
                name: "PK_TimeOffEntryUsers",
                table: "TimeOffEntryUsers",
                column: "Id"
            );

            migrationBuilder.AddPrimaryKey(
                name: "PK_TimeOffEntries",
                table: "TimeOffEntries",
                column: "Id"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffEntries_Events_EventId",
                table: "TimeOffEntries",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffEntries_LeaveTypes_LeaveTypeId",
                table: "TimeOffEntries",
                column: "LeaveTypeId",
                principalTable: "LeaveTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffEntries_TimeOffSeries_TimeOffSeriesId",
                table: "TimeOffEntries",
                column: "TimeOffSeriesId",
                principalTable: "TimeOffSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffEntries_Users_CreatedById",
                table: "TimeOffEntries",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffEntries_Users_UpdatedById",
                table: "TimeOffEntries",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffEntryUsers_TimeOffEntries_TimeOffEntryId",
                table: "TimeOffEntryUsers",
                column: "TimeOffEntryId",
                principalTable: "TimeOffEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffEntryUsers_Users_CreatedById",
                table: "TimeOffEntryUsers",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffEntryUsers_Users_UpdatedById",
                table: "TimeOffEntryUsers",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffEntryUsers_Users_UserId",
                table: "TimeOffEntryUsers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffSeries_EventSeries_EventSeriesId",
                table: "TimeOffSeries",
                column: "EventSeriesId",
                principalTable: "EventSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffSeries_LeaveTypes_LeaveTypeId",
                table: "TimeOffSeries",
                column: "LeaveTypeId",
                principalTable: "LeaveTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffSeries_Users_CreatedById",
                table: "TimeOffSeries",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffSeries_Users_UpdatedById",
                table: "TimeOffSeries",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffSeriesUsers_TimeOffSeries_TimeOffSeriesId",
                table: "TimeOffSeriesUsers",
                column: "TimeOffSeriesId",
                principalTable: "TimeOffSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffSeriesUsers_Users_CreatedById",
                table: "TimeOffSeriesUsers",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffSeriesUsers_Users_UpdatedById",
                table: "TimeOffSeriesUsers",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffSeriesUsers_Users_UserId",
                table: "TimeOffSeriesUsers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            // Reverse the identity sequence renames performed in Up().
            migrationBuilder.Sql(
                "ALTER SEQUENCE \"LeaveEntries_Id_seq\" RENAME TO \"TimeOffEntries_Id_seq\";"
            );
            migrationBuilder.Sql(
                "ALTER SEQUENCE \"LeaveEntryUsers_Id_seq\" RENAME TO \"TimeOffEntryUsers_Id_seq\";"
            );
            migrationBuilder.Sql(
                "ALTER SEQUENCE \"LeaveSeries_Id_seq\" RENAME TO \"TimeOffSeries_Id_seq\";"
            );
            migrationBuilder.Sql(
                "ALTER SEQUENCE \"LeaveSeriesUsers_Id_seq\" RENAME TO \"TimeOffSeriesUsers_Id_seq\";"
            );
        }
    }
}
