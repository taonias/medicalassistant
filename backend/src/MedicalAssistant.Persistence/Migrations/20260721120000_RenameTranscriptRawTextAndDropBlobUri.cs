using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalAssistant.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameTranscriptRawTextAndDropBlobUri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TranscriptBlobUri",
                table: "Transcripts");

            migrationBuilder.RenameColumn(
                name: "RawText",
                table: "Transcripts",
                newName: "Transcript");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Transcript",
                table: "Transcripts",
                newName: "RawText");

            migrationBuilder.AddColumn<string>(
                name: "TranscriptBlobUri",
                table: "Transcripts",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }
    }
}
