using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegalAcceptances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerSubject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TermsVersion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PrivacyVersion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalAcceptances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegalAcceptances_OwnerSubject",
                table: "LegalAcceptances",
                column: "OwnerSubject",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalAcceptances");
        }
    }
}
