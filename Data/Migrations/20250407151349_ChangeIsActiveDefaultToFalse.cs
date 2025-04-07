using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeIsActiveDefaultToFalse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false, // 👈 This is the important part
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true); // Only needed if EF previously tracked a default
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);
        }

    }
}
