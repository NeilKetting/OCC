using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryTypeToItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenceNo",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Template",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Terms",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "OrderLines",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "InventoryItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Data Migration: IsStockItem (bit) -> Type (int)
            // StockPart = 1, Service = 0
            migrationBuilder.Sql("UPDATE InventoryItems SET Type = 1 WHERE IsStockItem = 1");

            migrationBuilder.DropColumn(
                name: "IsStockItem",
                table: "InventoryItems");

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 29, 18, 50, 0, 456, DateTimeKind.Utc).AddTicks(3739));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("2d50946b-c807-4e9f-a74d-a6c5493b3c94"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 29, 18, 50, 0, 456, DateTimeKind.Utc).AddTicks(3733));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("3e473dfe-4182-4c81-8ba8-f5c33a9e1ed1"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 29, 18, 50, 0, 456, DateTimeKind.Utc).AddTicks(3735));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("496a7469-aa27-435d-899c-1a7c540f5187"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 29, 18, 50, 0, 456, DateTimeKind.Utc).AddTicks(3742));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("5eb30cce-ad23-43a9-9ca2-50236232dccf"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 29, 18, 50, 0, 456, DateTimeKind.Utc).AddTicks(3740));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("7f422560-941b-4fe4-80ef-b22adeddfbee"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 29, 18, 50, 0, 456, DateTimeKind.Utc).AddTicks(3737));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("80ce73e9-fd26-47db-b79f-57165ba68111"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 29, 18, 50, 0, 456, DateTimeKind.Utc).AddTicks(3736));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("a1e140e8-e1a8-4acf-b5e0-715ed41c7af3"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 29, 18, 50, 0, 456, DateTimeKind.Utc).AddTicks(3732));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b5b21171-4284-4f14-bfa4-e8bd0cdb3264"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 29, 18, 50, 0, 456, DateTimeKind.Utc).AddTicks(3741));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b862c2f5-9fe1-4228-9946-4d0aa0fdb12a"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 29, 18, 50, 0, 456, DateTimeKind.Utc).AddTicks(3409));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e226a941-9246-4dd5-91ec-7dff8a5a96ca"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 29, 18, 50, 0, 456, DateTimeKind.Utc).AddTicks(3738));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e91fa4f6-1b80-423b-8755-c8e133c34670"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 29, 18, 50, 0, 456, DateTimeKind.Utc).AddTicks(3734));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("fcc99eac-4678-49da-9e2e-f1026fe7c867"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 29, 18, 50, 0, 456, DateTimeKind.Utc).AddTicks(3743));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferenceNo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Template",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Terms",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "InventoryItems");

            migrationBuilder.AddColumn<bool>(
                name: "IsStockItem",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 21, 9, 46, 55, 33, DateTimeKind.Utc).AddTicks(9825));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("2d50946b-c807-4e9f-a74d-a6c5493b3c94"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 21, 9, 46, 55, 33, DateTimeKind.Utc).AddTicks(9811));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("3e473dfe-4182-4c81-8ba8-f5c33a9e1ed1"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 21, 9, 46, 55, 33, DateTimeKind.Utc).AddTicks(9813));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("496a7469-aa27-435d-899c-1a7c540f5187"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 21, 9, 46, 55, 33, DateTimeKind.Utc).AddTicks(9828));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("5eb30cce-ad23-43a9-9ca2-50236232dccf"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 21, 9, 46, 55, 33, DateTimeKind.Utc).AddTicks(9826));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("7f422560-941b-4fe4-80ef-b22adeddfbee"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 21, 9, 46, 55, 33, DateTimeKind.Utc).AddTicks(9815));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("80ce73e9-fd26-47db-b79f-57165ba68111"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 21, 9, 46, 55, 33, DateTimeKind.Utc).AddTicks(9814));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("a1e140e8-e1a8-4acf-b5e0-715ed41c7af3"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 21, 9, 46, 55, 33, DateTimeKind.Utc).AddTicks(9809));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b5b21171-4284-4f14-bfa4-e8bd0cdb3264"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 21, 9, 46, 55, 33, DateTimeKind.Utc).AddTicks(9827));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b862c2f5-9fe1-4228-9946-4d0aa0fdb12a"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 21, 9, 46, 55, 33, DateTimeKind.Utc).AddTicks(9428));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e226a941-9246-4dd5-91ec-7dff8a5a96ca"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 21, 9, 46, 55, 33, DateTimeKind.Utc).AddTicks(9824));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e91fa4f6-1b80-423b-8755-c8e133c34670"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 21, 9, 46, 55, 33, DateTimeKind.Utc).AddTicks(9812));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("fcc99eac-4678-49da-9e2e-f1026fe7c867"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 3, 21, 9, 46, 55, 33, DateTimeKind.Utc).AddTicks(9829));
        }
    }
}
