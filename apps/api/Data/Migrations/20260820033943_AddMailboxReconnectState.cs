using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMailboxReconnectState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresReconnect",
                table: "MailboxConnections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "MailboxConnections",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "RequiresReconnect",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiresReconnect",
                table: "MailboxConnections");
        }
    }
}
