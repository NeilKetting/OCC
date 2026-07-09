using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveDurationAndFractionalDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "NumberOfDays",
                table: "LeaveRequests",
                type: "float",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "DurationType",
                table: "LeaveRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "HoursRequested",
                table: "LeaveRequests",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PaidDays",
                table: "LeaveRequests",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "UnpaidDays",
                table: "LeaveRequests",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PaidLeaveHours",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "UnpaidLeaveHours",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 9, 10, 44, 22, 478, DateTimeKind.Utc).AddTicks(411));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("2d50946b-c807-4e9f-a74d-a6c5493b3c94"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 9, 10, 44, 22, 478, DateTimeKind.Utc).AddTicks(405));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("3e473dfe-4182-4c81-8ba8-f5c33a9e1ed1"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 9, 10, 44, 22, 478, DateTimeKind.Utc).AddTicks(407));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("496a7469-aa27-435d-899c-1a7c540f5187"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 9, 10, 44, 22, 478, DateTimeKind.Utc).AddTicks(413));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("5eb30cce-ad23-43a9-9ca2-50236232dccf"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 9, 10, 44, 22, 478, DateTimeKind.Utc).AddTicks(411));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("7f422560-941b-4fe4-80ef-b22adeddfbee"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 9, 10, 44, 22, 478, DateTimeKind.Utc).AddTicks(409));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("80ce73e9-fd26-47db-b79f-57165ba68111"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 9, 10, 44, 22, 478, DateTimeKind.Utc).AddTicks(408));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("a1e140e8-e1a8-4acf-b5e0-715ed41c7af3"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 9, 10, 44, 22, 478, DateTimeKind.Utc).AddTicks(403));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b5b21171-4284-4f14-bfa4-e8bd0cdb3264"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 9, 10, 44, 22, 478, DateTimeKind.Utc).AddTicks(412));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b862c2f5-9fe1-4228-9946-4d0aa0fdb12a"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 9, 10, 44, 22, 477, DateTimeKind.Utc).AddTicks(9919));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e226a941-9246-4dd5-91ec-7dff8a5a96ca"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 9, 10, 44, 22, 478, DateTimeKind.Utc).AddTicks(410));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e91fa4f6-1b80-423b-8755-c8e133c34670"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 9, 10, 44, 22, 478, DateTimeKind.Utc).AddTicks(406));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("fcc99eac-4678-49da-9e2e-f1026fe7c867"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 9, 10, 44, 22, 478, DateTimeKind.Utc).AddTicks(414));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationType",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "HoursRequested",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "PaidDays",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "UnpaidDays",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "PaidLeaveHours",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "UnpaidLeaveHours",
                table: "AttendanceRecords");

            migrationBuilder.AlterColumn<int>(
                name: "NumberOfDays",
                table: "LeaveRequests",
                type: "int",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 7, 14, 0, 55, 159, DateTimeKind.Utc).AddTicks(2316));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("2d50946b-c807-4e9f-a74d-a6c5493b3c94"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 7, 14, 0, 55, 159, DateTimeKind.Utc).AddTicks(2303));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("3e473dfe-4182-4c81-8ba8-f5c33a9e1ed1"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 7, 14, 0, 55, 159, DateTimeKind.Utc).AddTicks(2304));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("496a7469-aa27-435d-899c-1a7c540f5187"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 7, 14, 0, 55, 159, DateTimeKind.Utc).AddTicks(2319));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("5eb30cce-ad23-43a9-9ca2-50236232dccf"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 7, 14, 0, 55, 159, DateTimeKind.Utc).AddTicks(2317));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("7f422560-941b-4fe4-80ef-b22adeddfbee"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 7, 14, 0, 55, 159, DateTimeKind.Utc).AddTicks(2314));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("80ce73e9-fd26-47db-b79f-57165ba68111"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 7, 14, 0, 55, 159, DateTimeKind.Utc).AddTicks(2305));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("a1e140e8-e1a8-4acf-b5e0-715ed41c7af3"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 7, 14, 0, 55, 159, DateTimeKind.Utc).AddTicks(2301));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b5b21171-4284-4f14-bfa4-e8bd0cdb3264"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 7, 14, 0, 55, 159, DateTimeKind.Utc).AddTicks(2318));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b862c2f5-9fe1-4228-9946-4d0aa0fdb12a"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 7, 14, 0, 55, 159, DateTimeKind.Utc).AddTicks(1768));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e226a941-9246-4dd5-91ec-7dff8a5a96ca"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 7, 14, 0, 55, 159, DateTimeKind.Utc).AddTicks(2315));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e91fa4f6-1b80-423b-8755-c8e133c34670"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 7, 14, 0, 55, 159, DateTimeKind.Utc).AddTicks(2304));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("fcc99eac-4678-49da-9e2e-f1026fe7c867"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 7, 7, 14, 0, 55, 159, DateTimeKind.Utc).AddTicks(2319));
        }
    }
}
