using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GestorDocumentoApp.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalAndGitTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalAssigneeUserId",
                table: "ChangeRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalDecidedAt",
                table: "ChangeRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalDueAt",
                table: "ChangeRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalRequestedAt",
                table: "ChangeRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "ChangeRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GitTraceLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChangeRequestId = table.Column<int>(type: "integer", nullable: false),
                    VersionId = table.Column<int>(type: "integer", nullable: true),
                    Repository = table.Column<string>(type: "text", nullable: false),
                    CommitSha = table.Column<string>(type: "text", nullable: true),
                    PullRequestUrl = table.Column<string>(type: "text", nullable: true),
                    PullRequestNumber = table.Column<int>(type: "integer", nullable: true),
                    LinkedByUserId = table.Column<string>(type: "text", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitTraceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitTraceLinks_ChangeRequests_ChangeRequestId",
                        column: x => x.ChangeRequestId,
                        principalTable: "ChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GitTraceLinks_Versions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "Versions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GitTraceLinks_ChangeRequestId",
                table: "GitTraceLinks",
                column: "ChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_GitTraceLinks_VersionId",
                table: "GitTraceLinks",
                column: "VersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitTraceLinks");

            migrationBuilder.DropColumn(
                name: "ApprovalAssigneeUserId",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalDecidedAt",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalDueAt",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalRequestedAt",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "ChangeRequests");
        }
    }
}
