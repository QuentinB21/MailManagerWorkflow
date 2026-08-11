using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGmailConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderActionError",
                table: "ProcessingLogs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProviderLabelAppliedAt",
                table: "ProcessingLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConnectedAt",
                table: "MailboxConnections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailAddress",
                table: "MailboxConnections",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedRefreshToken",
                table: "MailboxConnections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrantedScopes",
                table: "MailboxConnections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncAt",
                table: "MailboxConnections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSyncError",
                table: "MailboxConnections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "MailboxConnections",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "ConnectedAt", "EmailAddress", "EncryptedRefreshToken", "GrantedScopes", "LastSyncAt", "LastSyncError" },
                values: new object[] { null, null, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderActionError",
                table: "ProcessingLogs");

            migrationBuilder.DropColumn(
                name: "ProviderLabelAppliedAt",
                table: "ProcessingLogs");

            migrationBuilder.DropColumn(
                name: "ConnectedAt",
                table: "MailboxConnections");

            migrationBuilder.DropColumn(
                name: "EmailAddress",
                table: "MailboxConnections");

            migrationBuilder.DropColumn(
                name: "EncryptedRefreshToken",
                table: "MailboxConnections");

            migrationBuilder.DropColumn(
                name: "GrantedScopes",
                table: "MailboxConnections");

            migrationBuilder.DropColumn(
                name: "LastSyncAt",
                table: "MailboxConnections");

            migrationBuilder.DropColumn(
                name: "LastSyncError",
                table: "MailboxConnections");
        }
    }
}
