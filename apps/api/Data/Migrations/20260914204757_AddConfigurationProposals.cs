using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurationProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfigurationProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MailboxConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerSubject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: false),
                    AfterJson = table.Column<string>(type: "text", nullable: false),
                    ChangedDestinationIdsJson = table.Column<string>(type: "text", nullable: false),
                    ChangedRuleIdsJson = table.Column<string>(type: "text", nullable: false),
                    WarningsJson = table.Column<string>(type: "text", nullable: false),
                    SynchronizationStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfigurationProposals_MailboxConnections_MailboxConnection~",
                        column: x => x.MailboxConnectionId,
                        principalTable: "MailboxConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationProposals_MailboxConnectionId",
                table: "ConfigurationProposals",
                column: "MailboxConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationProposals_OwnerSubject_CreatedAt",
                table: "ConfigurationProposals",
                columns: new[] { "OwnerSubject", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigurationProposals");
        }
    }
}
