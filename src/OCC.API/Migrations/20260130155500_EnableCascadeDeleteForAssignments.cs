using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class EnableCascadeDeleteForAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignments_ProjectTasks_TaskId",
                table: "TaskAssignments");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAssignments_ProjectTasks_TaskId",
                table: "TaskAssignments",
                column: "TaskId",
                principalTable: "ProjectTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignments_ProjectTasks_TaskId",
                table: "TaskAssignments");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAssignments_ProjectTasks_TaskId",
                table: "TaskAssignments",
                column: "TaskId",
                principalTable: "ProjectTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
