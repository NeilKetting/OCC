using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyTaskColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF COL_LENGTH('ProjectTasks', 'AssignedTo') IS NULL BEGIN ALTER TABLE [ProjectTasks] ADD [AssignedTo] nvarchar(max) NOT NULL DEFAULT N''; END");

            migrationBuilder.Sql("IF COL_LENGTH('ProjectTasks', 'PlanedDurationHours') IS NULL BEGIN ALTER TABLE [ProjectTasks] ADD [PlanedDurationHours] bigint NULL; END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedTo",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "PlanedDurationHours",
                table: "ProjectTasks");
        }
    }
}
