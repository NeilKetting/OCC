using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVariationOrderDurationAndTaskLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                table: "ProjectVariationOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "VariationOrderId",
                table: "ProjectTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 24, 5, 50, 34, 50, DateTimeKind.Utc).AddTicks(1300));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("2d50946b-c807-4e9f-a74d-a6c5493b3c94"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 24, 5, 50, 34, 50, DateTimeKind.Utc).AddTicks(1294));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("3e473dfe-4182-4c81-8ba8-f5c33a9e1ed1"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 24, 5, 50, 34, 50, DateTimeKind.Utc).AddTicks(1297));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("496a7469-aa27-435d-899c-1a7c540f5187"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 24, 5, 50, 34, 50, DateTimeKind.Utc).AddTicks(1314));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("5eb30cce-ad23-43a9-9ca2-50236232dccf"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 24, 5, 50, 34, 50, DateTimeKind.Utc).AddTicks(1301));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("7f422560-941b-4fe4-80ef-b22adeddfbee"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 24, 5, 50, 34, 50, DateTimeKind.Utc).AddTicks(1298));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("80ce73e9-fd26-47db-b79f-57165ba68111"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 24, 5, 50, 34, 50, DateTimeKind.Utc).AddTicks(1297));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("a1e140e8-e1a8-4acf-b5e0-715ed41c7af3"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 24, 5, 50, 34, 50, DateTimeKind.Utc).AddTicks(1293));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b5b21171-4284-4f14-bfa4-e8bd0cdb3264"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 24, 5, 50, 34, 50, DateTimeKind.Utc).AddTicks(1302));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b862c2f5-9fe1-4228-9946-4d0aa0fdb12a"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 24, 5, 50, 34, 50, DateTimeKind.Utc).AddTicks(735));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e226a941-9246-4dd5-91ec-7dff8a5a96ca"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 24, 5, 50, 34, 50, DateTimeKind.Utc).AddTicks(1299));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e91fa4f6-1b80-423b-8755-c8e133c34670"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 24, 5, 50, 34, 50, DateTimeKind.Utc).AddTicks(1296));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("fcc99eac-4678-49da-9e2e-f1026fe7c867"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 24, 5, 50, 34, 50, DateTimeKind.Utc).AddTicks(1315));

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_VariationOrderId",
                table: "ProjectTasks",
                column: "VariationOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTasks_ProjectVariationOrders_VariationOrderId",
                table: "ProjectTasks",
                column: "VariationOrderId",
                principalTable: "ProjectVariationOrders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTasks_ProjectVariationOrders_VariationOrderId",
                table: "ProjectTasks");

            migrationBuilder.DropIndex(
                name: "IX_ProjectTasks_VariationOrderId",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "VariationOrderId",
                table: "ProjectTasks");

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 15, 28, 54, 498, DateTimeKind.Utc).AddTicks(7961));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("2d50946b-c807-4e9f-a74d-a6c5493b3c94"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 15, 28, 54, 498, DateTimeKind.Utc).AddTicks(7955));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("3e473dfe-4182-4c81-8ba8-f5c33a9e1ed1"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 15, 28, 54, 498, DateTimeKind.Utc).AddTicks(7957));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("496a7469-aa27-435d-899c-1a7c540f5187"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 15, 28, 54, 498, DateTimeKind.Utc).AddTicks(7964));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("5eb30cce-ad23-43a9-9ca2-50236232dccf"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 15, 28, 54, 498, DateTimeKind.Utc).AddTicks(7962));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("7f422560-941b-4fe4-80ef-b22adeddfbee"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 15, 28, 54, 498, DateTimeKind.Utc).AddTicks(7959));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("80ce73e9-fd26-47db-b79f-57165ba68111"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 15, 28, 54, 498, DateTimeKind.Utc).AddTicks(7958));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("a1e140e8-e1a8-4acf-b5e0-715ed41c7af3"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 15, 28, 54, 498, DateTimeKind.Utc).AddTicks(7954));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b5b21171-4284-4f14-bfa4-e8bd0cdb3264"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 15, 28, 54, 498, DateTimeKind.Utc).AddTicks(7963));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b862c2f5-9fe1-4228-9946-4d0aa0fdb12a"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 15, 28, 54, 498, DateTimeKind.Utc).AddTicks(7419));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e226a941-9246-4dd5-91ec-7dff8a5a96ca"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 15, 28, 54, 498, DateTimeKind.Utc).AddTicks(7960));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e91fa4f6-1b80-423b-8755-c8e133c34670"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 15, 28, 54, 498, DateTimeKind.Utc).AddTicks(7956));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("fcc99eac-4678-49da-9e2e-f1026fe7c867"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 15, 28, 54, 498, DateTimeKind.Utc).AddTicks(7965));
        }
    }
}
