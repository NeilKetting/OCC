using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class FixTaskIdMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskComments_ProjectTasks_ProjectTaskId",
                table: "TaskComments");

            migrationBuilder.DropIndex(
                name: "IX_TaskComments_ProjectTaskId",
                table: "TaskComments");

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("17ca9900-521b-4829-9ac3-5590f3727467"));

            migrationBuilder.DropColumn(
                name: "ProjectTaskId",
                table: "TaskComments");

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"),
                columns: new[] { "Date", "Name" },
                values: new object[] { new DateTime(2026, 8, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "Public Holiday" });

            migrationBuilder.InsertData(
                table: "PublicHolidays",
                columns: new[] { "Id", "Date", "Name" },
                values: new object[] { new Guid("496a7469-aa27-435d-899c-1a7c540f5187"), new DateTime(2026, 12, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), "Christmas Day" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("496a7469-aa27-435d-899c-1a7c540f5187"));

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectTaskId",
                table: "TaskComments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"),
                columns: new[] { "Date", "Name" },
                values: new object[] { new DateTime(2026, 12, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), "Christmas Day" });

            migrationBuilder.InsertData(
                table: "PublicHolidays",
                columns: new[] { "Id", "Date", "Name" },
                values: new object[] { new Guid("17ca9900-521b-4829-9ac3-5590f3727467"), new DateTime(2026, 8, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "Public Holiday" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_ProjectTaskId",
                table: "TaskComments",
                column: "ProjectTaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskComments_ProjectTasks_ProjectTaskId",
                table: "TaskComments",
                column: "ProjectTaskId",
                principalTable: "ProjectTasks",
                principalColumn: "Id");
        }
    }
}
