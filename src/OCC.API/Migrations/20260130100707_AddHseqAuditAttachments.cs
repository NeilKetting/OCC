using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class AddHseqAuditAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Findings",
                table: "HseqAudits");

            migrationBuilder.DropColumn(
                name: "ImmediateAction",
                table: "HseqAudits");

            migrationBuilder.DropColumn(
                name: "NonConformance",
                table: "HseqAudits");

            migrationBuilder.DropColumn(
                name: "PhotoBase64",
                table: "HseqAuditNonComplianceItems");

            migrationBuilder.CreateTable(
                name: "HseqAuditAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NonComplianceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileSize = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HseqAuditAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HseqAuditAttachments_HseqAuditNonComplianceItems_NonComplianceItemId",
                        column: x => x.NonComplianceItemId,
                        principalTable: "HseqAuditNonComplianceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HseqAuditAttachments_HseqAudits_AuditId",
                        column: x => x.AuditId,
                        principalTable: "HseqAudits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HseqAuditAttachments_AuditId",
                table: "HseqAuditAttachments",
                column: "AuditId");

            migrationBuilder.CreateIndex(
                name: "IX_HseqAuditAttachments_NonComplianceItemId",
                table: "HseqAuditAttachments",
                column: "NonComplianceItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HseqAuditAttachments");

            migrationBuilder.AddColumn<string>(
                name: "Findings",
                table: "HseqAudits",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ImmediateAction",
                table: "HseqAudits",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NonConformance",
                table: "HseqAudits",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhotoBase64",
                table: "HseqAuditNonComplianceItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
