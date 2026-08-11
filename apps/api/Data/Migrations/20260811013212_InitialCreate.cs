using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MailboxConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailboxConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabelDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MailboxConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ExternalLabelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabelDefinitions_MailboxConnections_MailboxConnectionId",
                        column: x => x.MailboxConnectionId,
                        principalTable: "MailboxConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MailboxConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalMessageId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsClassified = table.Column<bool>(type: "boolean", nullable: false),
                    DestinationLabelId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchedRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    DestinationLabelName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    MatchedRuleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MatchedCriteria = table.Column<string[]>(type: "text[]", nullable: false),
                    NoMatchReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessingLogs_MailboxConnections_MailboxConnectionId",
                        column: x => x.MailboxConnectionId,
                        principalTable: "MailboxConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassificationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MailboxConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationLabelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MatchMode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SenderAddresses = table.Column<string[]>(type: "text[]", nullable: false),
                    SenderDomains = table.Column<string[]>(type: "text[]", nullable: false),
                    SubjectKeywords = table.Column<string[]>(type: "text[]", nullable: false),
                    BodyKeywords = table.Column<string[]>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassificationRules_LabelDefinitions_DestinationLabelId",
                        column: x => x.DestinationLabelId,
                        principalTable: "LabelDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassificationRules_MailboxConnections_MailboxConnectionId",
                        column: x => x.MailboxConnectionId,
                        principalTable: "MailboxConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MailboxConnections",
                columns: new[] { "Id", "CreatedAt", "DisplayName", "IsActive", "Provider" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Boîte Gmail de démonstration", true, "Gmail" });

            migrationBuilder.InsertData(
                table: "LabelDefinitions",
                columns: new[] { "Id", "Color", "CreatedAt", "ExternalLabelId", "IsActive", "MailboxConnectionId", "Name" },
                values: new object[] { new Guid("22222222-2222-2222-2222-222222222222"), "#2563eb", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, new Guid("11111111-1111-1111-1111-111111111111"), "Projet Démo" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationRules_DestinationLabelId",
                table: "ClassificationRules",
                column: "DestinationLabelId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationRules_MailboxConnectionId_Priority",
                table: "ClassificationRules",
                columns: new[] { "MailboxConnectionId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_LabelDefinitions_MailboxConnectionId_Name",
                table: "LabelDefinitions",
                columns: new[] { "MailboxConnectionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLogs_MailboxConnectionId_ExternalMessageId",
                table: "ProcessingLogs",
                columns: new[] { "MailboxConnectionId", "ExternalMessageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassificationRules");

            migrationBuilder.DropTable(
                name: "ProcessingLogs");

            migrationBuilder.DropTable(
                name: "LabelDefinitions");

            migrationBuilder.DropTable(
                name: "MailboxConnections");
        }
    }
}
