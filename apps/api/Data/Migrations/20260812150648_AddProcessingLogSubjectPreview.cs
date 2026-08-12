using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingLogSubjectPreview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS also repairs local databases where a discarded migration
            // already created the column without leaving a matching migration file.
            migrationBuilder.Sql(
                "ALTER TABLE \"ProcessingLogs\" ADD COLUMN IF NOT EXISTS \"SubjectPreview\" character varying(250);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"ProcessingLogs\" DROP COLUMN IF EXISTS \"SubjectPreview\";");
        }
    }
}
