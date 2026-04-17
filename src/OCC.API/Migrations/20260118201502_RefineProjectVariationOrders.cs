using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class RefineProjectVariationOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("1a9aea46-17a0-4519-a02e-f6187f27141c"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("327aa165-ce9a-476d-829c-352701c347a4"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("40d901f5-1d62-48ce-ad7f-b1ce0b50c248"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("496a7469-aa27-435d-899c-1a7c540f5187"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("6de90cc9-0138-4ac4-a995-f11827440cd1"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("83b93df2-95d6-443b-9c91-00e548c6d1cd"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("92cba65b-f9e6-47dc-b2e5-eebe6a8d374e"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("a4e19ec4-f0f4-4d97-9bf9-78e3a51c1f2e"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("ab6c9d08-1667-438a-ad20-593b5bafc0aa"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("d9c78abe-ae8f-4691-9c80-63b5f67db36c"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("f025206c-d146-4f39-a719-9afe02b3fd42"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("f05a8bbf-b412-47b8-a81d-722427e9cea6"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("f51f81dd-aee9-4a58-a956-cfb86fe70150"));

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "ProjectVariationOrders");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ProjectVariationOrders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.InsertData(
                table: "PublicHolidays",
                columns: new[] { "Id", "Date", "Name" },
                values: new object[,]
                {
                    { new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"), new DateTime(2026, 12, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), "Christmas Day" },
                    { new Guid("17ca9900-521b-4829-9ac3-5590f3727467"), new DateTime(2026, 8, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "Public Holiday" },
                    { new Guid("2d50946b-c807-4e9f-a74d-a6c5493b3c94"), new DateTime(2026, 4, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), "Good Friday" },
                    { new Guid("3e473dfe-4182-4c81-8ba8-f5c33a9e1ed1"), new DateTime(2026, 4, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), "Freedom Day" },
                    { new Guid("5eb30cce-ad23-43a9-9ca2-50236232dccf"), new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), "Heritage Day" },
                    { new Guid("7f422560-941b-4fe4-80ef-b22adeddfbee"), new DateTime(2026, 6, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), "Youth Day" },
                    { new Guid("80ce73e9-fd26-47db-b79f-57165ba68111"), new DateTime(2026, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Workers' Day" },
                    { new Guid("a1e140e8-e1a8-4acf-b5e0-715ed41c7af3"), new DateTime(2026, 3, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), "Human Rights Day" },
                    { new Guid("b5b21171-4284-4f14-bfa4-e8bd0cdb3264"), new DateTime(2026, 12, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), "Day of Reconciliation" },
                    { new Guid("b862c2f5-9fe1-4228-9946-4d0aa0fdb12a"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "New Year's Day" },
                    { new Guid("e226a941-9246-4dd5-91ec-7dff8a5a96ca"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), "National Women's Day" },
                    { new Guid("e91fa4f6-1b80-423b-8755-c8e133c34670"), new DateTime(2026, 4, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), "Family Day" },
                    { new Guid("fcc99eac-4678-49da-9e2e-f1026fe7c867"), new DateTime(2026, 12, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), "Day of Goodwill" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("17ca9900-521b-4829-9ac3-5590f3727467"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("2d50946b-c807-4e9f-a74d-a6c5493b3c94"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("3e473dfe-4182-4c81-8ba8-f5c33a9e1ed1"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("5eb30cce-ad23-43a9-9ca2-50236232dccf"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("7f422560-941b-4fe4-80ef-b22adeddfbee"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("80ce73e9-fd26-47db-b79f-57165ba68111"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("a1e140e8-e1a8-4acf-b5e0-715ed41c7af3"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b5b21171-4284-4f14-bfa4-e8bd0cdb3264"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b862c2f5-9fe1-4228-9946-4d0aa0fdb12a"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e226a941-9246-4dd5-91ec-7dff8a5a96ca"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e91fa4f6-1b80-423b-8755-c8e133c34670"));

            migrationBuilder.DeleteData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("fcc99eac-4678-49da-9e2e-f1026fe7c867"));

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "ProjectVariationOrders",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "ProjectVariationOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.InsertData(
                table: "PublicHolidays",
                columns: new[] { "Id", "Date", "Name" },
                values: new object[,]
                {
                    { new Guid("1a9aea46-17a0-4519-a02e-f6187f27141c"), new DateTime(2026, 12, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), "Day of Reconciliation" },
                    { new Guid("327aa165-ce9a-476d-829c-352701c347a4"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), "National Women's Day" },
                    { new Guid("40d901f5-1d62-48ce-ad7f-b1ce0b50c248"), new DateTime(2026, 12, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), "Day of Goodwill" },
                    { new Guid("496a7469-aa27-435d-899c-1a7c540f5187"), new DateTime(2026, 12, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), "Christmas Day" },
                    { new Guid("6de90cc9-0138-4ac4-a995-f11827440cd1"), new DateTime(2026, 4, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), "Family Day" },
                    { new Guid("83b93df2-95d6-443b-9c91-00e548c6d1cd"), new DateTime(2026, 3, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), "Human Rights Day" },
                    { new Guid("92cba65b-f9e6-47dc-b2e5-eebe6a8d374e"), new DateTime(2026, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Workers' Day" },
                    { new Guid("a4e19ec4-f0f4-4d97-9bf9-78e3a51c1f2e"), new DateTime(2026, 8, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "Public Holiday" },
                    { new Guid("ab6c9d08-1667-438a-ad20-593b5bafc0aa"), new DateTime(2026, 4, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), "Good Friday" },
                    { new Guid("d9c78abe-ae8f-4691-9c80-63b5f67db36c"), new DateTime(2026, 4, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), "Freedom Day" },
                    { new Guid("f025206c-d146-4f39-a719-9afe02b3fd42"), new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), "Heritage Day" },
                    { new Guid("f05a8bbf-b412-47b8-a81d-722427e9cea6"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "New Year's Day" },
                    { new Guid("f51f81dd-aee9-4a58-a956-cfb86fe70150"), new DateTime(2026, 6, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), "Youth Day" }
                });
        }
    }
}
