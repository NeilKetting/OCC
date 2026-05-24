using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HseqDocuments_Projects_ProjectId",
                table: "HseqDocuments");

            migrationBuilder.CreateTable(
                name: "ProjectReportDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneralWasteTon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RubbleM3 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScrapMetalsTon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AsbestosTon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SiteEstablishmentPlanned = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SiteEstablishmentActual = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PracticalCompletionPlanned = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PracticalCompletionActual = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PowPercentRequired = table.Column<double>(type: "float", nullable: false),
                    DelayDays = table.Column<int>(type: "int", nullable: false),
                    StreamingPlanned = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StreamingActual = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectReportDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectReportDrafts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectReportHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileSize = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WeekNumber = table.Column<int>(type: "int", nullable: false),
                    GeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectReportHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectReportHistories_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 6, 0, 20, 183, DateTimeKind.Utc).AddTicks(5480));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("2d50946b-c807-4e9f-a74d-a6c5493b3c94"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 6, 0, 20, 183, DateTimeKind.Utc).AddTicks(5474));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("3e473dfe-4182-4c81-8ba8-f5c33a9e1ed1"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 6, 0, 20, 183, DateTimeKind.Utc).AddTicks(5476));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("496a7469-aa27-435d-899c-1a7c540f5187"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 6, 0, 20, 183, DateTimeKind.Utc).AddTicks(5482));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("5eb30cce-ad23-43a9-9ca2-50236232dccf"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 6, 0, 20, 183, DateTimeKind.Utc).AddTicks(5481));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("7f422560-941b-4fe4-80ef-b22adeddfbee"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 6, 0, 20, 183, DateTimeKind.Utc).AddTicks(5478));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("80ce73e9-fd26-47db-b79f-57165ba68111"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 6, 0, 20, 183, DateTimeKind.Utc).AddTicks(5477));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("a1e140e8-e1a8-4acf-b5e0-715ed41c7af3"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 6, 0, 20, 183, DateTimeKind.Utc).AddTicks(5472));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b5b21171-4284-4f14-bfa4-e8bd0cdb3264"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 6, 0, 20, 183, DateTimeKind.Utc).AddTicks(5481));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b862c2f5-9fe1-4228-9946-4d0aa0fdb12a"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 6, 0, 20, 183, DateTimeKind.Utc).AddTicks(4889));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e226a941-9246-4dd5-91ec-7dff8a5a96ca"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 6, 0, 20, 183, DateTimeKind.Utc).AddTicks(5479));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e91fa4f6-1b80-423b-8755-c8e133c34670"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 6, 0, 20, 183, DateTimeKind.Utc).AddTicks(5475));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("fcc99eac-4678-49da-9e2e-f1026fe7c867"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 22, 6, 0, 20, 183, DateTimeKind.Utc).AddTicks(5483));

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_ProjectId",
                table: "Incidents",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_HseqAudits_ProjectId",
                table: "HseqAudits",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReportDrafts_ProjectId",
                table: "ProjectReportDrafts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReportHistories_ProjectId",
                table: "ProjectReportHistories",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_HseqAudits_Projects_ProjectId",
                table: "HseqAudits",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HseqDocuments_Projects_ProjectId",
                table: "HseqDocuments",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Projects_ProjectId",
                table: "Incidents",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HseqAudits_Projects_ProjectId",
                table: "HseqAudits");

            migrationBuilder.DropForeignKey(
                name: "FK_HseqDocuments_Projects_ProjectId",
                table: "HseqDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Projects_ProjectId",
                table: "Incidents");

            migrationBuilder.DropTable(
                name: "ProjectReportDrafts");

            migrationBuilder.DropTable(
                name: "ProjectReportHistories");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_ProjectId",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_HseqAudits_ProjectId",
                table: "HseqAudits");
            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 6, 6, 18, 34, 47, DateTimeKind.Utc).AddTicks(2248));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("2d50946b-c807-4e9f-a74d-a6c5493b3c94"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 6, 6, 18, 34, 47, DateTimeKind.Utc).AddTicks(2237));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("3e473dfe-4182-4c81-8ba8-f5c33a9e1ed1"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 6, 6, 18, 34, 47, DateTimeKind.Utc).AddTicks(2243));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("496a7469-aa27-435d-899c-1a7c540f5187"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 6, 6, 18, 34, 47, DateTimeKind.Utc).AddTicks(2252));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("5eb30cce-ad23-43a9-9ca2-50236232dccf"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 6, 6, 18, 34, 47, DateTimeKind.Utc).AddTicks(2249));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("7f422560-941b-4fe4-80ef-b22adeddfbee"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 6, 6, 18, 34, 47, DateTimeKind.Utc).AddTicks(2245));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("80ce73e9-fd26-47db-b79f-57165ba68111"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 6, 6, 18, 34, 47, DateTimeKind.Utc).AddTicks(2244));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("a1e140e8-e1a8-4acf-b5e0-715ed41c7af3"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 6, 6, 18, 34, 47, DateTimeKind.Utc).AddTicks(2230));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b5b21171-4284-4f14-bfa4-e8bd0cdb3264"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 6, 6, 18, 34, 47, DateTimeKind.Utc).AddTicks(2251));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b862c2f5-9fe1-4228-9946-4d0aa0fdb12a"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 6, 6, 18, 34, 47, DateTimeKind.Utc).AddTicks(684));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e226a941-9246-4dd5-91ec-7dff8a5a96ca"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 6, 6, 18, 34, 47, DateTimeKind.Utc).AddTicks(2247));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e91fa4f6-1b80-423b-8755-c8e133c34670"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 6, 6, 18, 34, 47, DateTimeKind.Utc).AddTicks(2240));

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("fcc99eac-4678-49da-9e2e-f1026fe7c867"),
                column: "CreatedAtUtc",
                value: new DateTime(2026, 5, 6, 6, 18, 34, 47, DateTimeKind.Utc).AddTicks(2253));

            migrationBuilder.AddForeignKey(
                name: "FK_HseqDocuments_Projects_ProjectId",
                table: "HseqDocuments",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");
        }
    }
}
