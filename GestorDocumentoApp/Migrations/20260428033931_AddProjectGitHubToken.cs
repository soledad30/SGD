using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorDocumentoApp.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectGitHubToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GitHubTokenCipherText",
                table: "Projects",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GitHubTokenCipherText",
                table: "Projects");
        }
    }
}
