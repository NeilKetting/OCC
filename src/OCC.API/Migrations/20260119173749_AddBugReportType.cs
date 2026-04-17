using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBugReportType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OvertimeHours",
                table: "WageRunLines",
                newName: "Overtime20Hours");

            migrationBuilder.AddColumn<double>(
                name: "LunchDeductionHours",
                table: "WageRunLines",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Overtime15Hours",
                table: "WageRunLines",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "BugReports",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LunchDeductionHours",
                table: "WageRunLines");

            migrationBuilder.DropColumn(
                name: "Overtime15Hours",
                table: "WageRunLines");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "BugReports");

            migrationBuilder.RenameColumn(
                name: "Overtime20Hours",
                table: "WageRunLines",
                newName: "OvertimeHours");
        }
    }
}
