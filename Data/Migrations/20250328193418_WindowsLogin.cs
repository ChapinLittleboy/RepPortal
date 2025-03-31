using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class WindowsLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WindowsLogin",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WindowsLogin",
                table: "AspNetUsers");
        }
    }
}
