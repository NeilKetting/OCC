using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationToProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HseqAuditAttachments_HseqAudits_AuditId",
                table: "HseqAuditAttachments");

            migrationBuilder.Sql("IF COL_LENGTH('Projects', 'Location') IS NULL BEGIN ALTER TABLE [Projects] ADD [Location] nvarchar(max) NOT NULL DEFAULT N''; END");

            migrationBuilder.AddForeignKey(
                name: "FK_HseqAuditAttachments_HseqAudits_AuditId",
                table: "HseqAuditAttachments",
                column: "AuditId",
                principalTable: "HseqAudits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HseqAuditAttachments_HseqAudits_AuditId",
                table: "HseqAuditAttachments");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Projects");

            migrationBuilder.AddForeignKey(
                name: "FK_HseqAuditAttachments_HseqAudits_AuditId",
                table: "HseqAuditAttachments",
                column: "AuditId",
                principalTable: "HseqAudits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
