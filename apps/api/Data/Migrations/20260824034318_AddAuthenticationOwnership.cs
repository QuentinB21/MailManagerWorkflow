using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerSubject",
                table: "MailboxConnections",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // Every pre-authentication mailbox belongs to the local owner account.
            // The public demo mailbox is inserted separately below and never reuses OAuth data.
            migrationBuilder.Sql(
                "UPDATE \"MailboxConnections\" SET \"OwnerSubject\" = '10000000-0000-0000-0000-000000000001'");

            migrationBuilder.UpdateData(
                table: "MailboxConnections",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "OwnerSubject",
                value: "10000000-0000-0000-0000-000000000001");

            migrationBuilder.AlterColumn<string>(
                name: "OwnerSubject",
                table: "MailboxConnections",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.InsertData(
                table: "MailboxConnections",
                columns: new[] { "Id", "ConnectedAt", "CreatedAt", "DisplayName", "EmailAddress", "EncryptedRefreshToken", "GrantedScopes", "IsActive", "LastSyncAt", "LastSyncError", "OwnerSubject", "Provider", "RequiresReconnect" },
                values: new object[] { new Guid("44444444-4444-4444-4444-444444444444"), null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Boîte de démonstration", null, null, null, true, null, null, "10000000-0000-0000-0000-000000000002", "Gmail", false });

            migrationBuilder.InsertData(
                table: "LabelDefinitions",
                columns: new[] { "Id", "Color", "CreatedAt", "ExternalLabelId", "IsActive", "MailboxConnectionId", "Name" },
                values: new object[] { new Guid("55555555-5555-5555-5555-555555555555"), "#c64a2f", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, new Guid("44444444-4444-4444-4444-444444444444"), "Projet Démo" });

            migrationBuilder.InsertData(
                table: "ClassificationRules",
                columns: new[] { "Id", "BodyKeywords", "CreatedAt", "DestinationLabelId", "IsActive", "MailboxConnectionId", "MatchMode", "Name", "Priority", "SenderAddresses", "SenderDomains", "SubjectKeywords", "UpdatedAt" },
                values: new object[] { new Guid("66666666-6666-6666-6666-666666666666"), new string[0], new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("55555555-5555-5555-5555-555555555555"), true, new Guid("44444444-4444-4444-4444-444444444444"), "Any", "Projet Alpha", 10, new string[0], new[] { "client.fr" }, new[] { "projet alpha" }, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "IX_MailboxConnections_OwnerSubject",
                table: "MailboxConnections",
                column: "OwnerSubject");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MailboxConnections_OwnerSubject",
                table: "MailboxConnections");

            migrationBuilder.DeleteData(
                table: "ClassificationRules",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"));

            migrationBuilder.DeleteData(
                table: "LabelDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"));

            migrationBuilder.DeleteData(
                table: "MailboxConnections",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"));

            migrationBuilder.DropColumn(
                name: "OwnerSubject",
                table: "MailboxConnections");
        }
    }
}
