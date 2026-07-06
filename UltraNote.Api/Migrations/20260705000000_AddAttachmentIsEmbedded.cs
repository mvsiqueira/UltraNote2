using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltraNote.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentIsEmbedded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEmbedded",
                table: "Attachments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEmbedded",
                table: "Attachments");
        }
    }
}
