using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGlobalReorderPoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ReorderPoint",
                table: "InventoryItems",
                newName: "JhbReorderPoint");

            migrationBuilder.AddColumn<double>(
                name: "CptReorderPoint",
                table: "InventoryItems",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CptReorderPoint",
                table: "InventoryItems");

            migrationBuilder.RenameColumn(
                name: "JhbReorderPoint",
                table: "InventoryItems",
                newName: "ReorderPoint");
        }
    }
}
