using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class AddWageRunEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Branch",
                table: "WageRuns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("IF COL_LENGTH('WageRunLines', 'DeductionGas') IS NULL ALTER TABLE [WageRunLines] ADD [DeductionGas] decimal(18,2) NOT NULL DEFAULT 0.0;");
            migrationBuilder.Sql("IF COL_LENGTH('WageRunLines', 'DeductionWashing') IS NULL ALTER TABLE [WageRunLines] ADD [DeductionWashing] decimal(18,2) NOT NULL DEFAULT 0.0;");

            migrationBuilder.AddColumn<bool>(
                name: "LivesInCompanyHousing",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 1, 6, 22, 40, 516, DateTimeKind.Utc).AddTicks(4572));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("2d50946b-c807-4e9f-a74d-a6c5493b3c94"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 1, 6, 22, 40, 516, DateTimeKind.Utc).AddTicks(4559));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("3e473dfe-4182-4c81-8ba8-f5c33a9e1ed1"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 1, 6, 22, 40, 516, DateTimeKind.Utc).AddTicks(4567));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("496a7469-aa27-435d-899c-1a7c540f5187"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 1, 6, 22, 40, 516, DateTimeKind.Utc).AddTicks(4577));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("5eb30cce-ad23-43a9-9ca2-50236232dccf"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 1, 6, 22, 40, 516, DateTimeKind.Utc).AddTicks(4573));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("7f422560-941b-4fe4-80ef-b22adeddfbee"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 1, 6, 22, 40, 516, DateTimeKind.Utc).AddTicks(4570));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("80ce73e9-fd26-47db-b79f-57165ba68111"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 1, 6, 22, 40, 516, DateTimeKind.Utc).AddTicks(4568));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("a1e140e8-e1a8-4acf-b5e0-715ed41c7af3"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 1, 6, 22, 40, 516, DateTimeKind.Utc).AddTicks(4557));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b5b21171-4284-4f14-bfa4-e8bd0cdb3264"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 1, 6, 22, 40, 516, DateTimeKind.Utc).AddTicks(4574));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b862c2f5-9fe1-4228-9946-4d0aa0fdb12a"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 1, 6, 22, 40, 516, DateTimeKind.Utc).AddTicks(4157));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e226a941-9246-4dd5-91ec-7dff8a5a96ca"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 1, 6, 22, 40, 516, DateTimeKind.Utc).AddTicks(4571));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e91fa4f6-1b80-423b-8755-c8e133c34670"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 1, 6, 22, 40, 516, DateTimeKind.Utc).AddTicks(4565));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("fcc99eac-4678-49da-9e2e-f1026fe7c867"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 1, 6, 22, 40, 516, DateTimeKind.Utc).AddTicks(4578));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Branch",
                table: "WageRuns");

            migrationBuilder.DropColumn(
                name: "DeductionGas",
                table: "WageRunLines");

            migrationBuilder.DropColumn(
                name: "DeductionWashing",
                table: "WageRunLines");

            migrationBuilder.DropColumn(
                name: "LivesInCompanyHousing",
                table: "Employees");

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 2, 28, 12, 15, 35, 17, DateTimeKind.Utc).AddTicks(5128));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("2d50946b-c807-4e9f-a74d-a6c5493b3c94"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 2, 28, 12, 15, 35, 17, DateTimeKind.Utc).AddTicks(5111));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("3e473dfe-4182-4c81-8ba8-f5c33a9e1ed1"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 2, 28, 12, 15, 35, 17, DateTimeKind.Utc).AddTicks(5114));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("496a7469-aa27-435d-899c-1a7c540f5187"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 2, 28, 12, 15, 35, 17, DateTimeKind.Utc).AddTicks(5140));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("5eb30cce-ad23-43a9-9ca2-50236232dccf"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 2, 28, 12, 15, 35, 17, DateTimeKind.Utc).AddTicks(5138));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("7f422560-941b-4fe4-80ef-b22adeddfbee"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 2, 28, 12, 15, 35, 17, DateTimeKind.Utc).AddTicks(5116));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("80ce73e9-fd26-47db-b79f-57165ba68111"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 2, 28, 12, 15, 35, 17, DateTimeKind.Utc).AddTicks(5115));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("a1e140e8-e1a8-4acf-b5e0-715ed41c7af3"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 2, 28, 12, 15, 35, 17, DateTimeKind.Utc).AddTicks(5109));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b5b21171-4284-4f14-bfa4-e8bd0cdb3264"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 2, 28, 12, 15, 35, 17, DateTimeKind.Utc).AddTicks(5139));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b862c2f5-9fe1-4228-9946-4d0aa0fdb12a"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 2, 28, 12, 15, 35, 17, DateTimeKind.Utc).AddTicks(4725));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e226a941-9246-4dd5-91ec-7dff8a5a96ca"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 2, 28, 12, 15, 35, 17, DateTimeKind.Utc).AddTicks(5127));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e91fa4f6-1b80-423b-8755-c8e133c34670"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 2, 28, 12, 15, 35, 17, DateTimeKind.Utc).AddTicks(5112));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("fcc99eac-4678-49da-9e2e-f1026fe7c867"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 2, 28, 12, 15, 35, 17, DateTimeKind.Utc).AddTicks(5142));
        }
    }
}
