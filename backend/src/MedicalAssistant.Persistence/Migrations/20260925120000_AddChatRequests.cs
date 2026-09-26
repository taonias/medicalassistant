using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MedicalAssistant.Persistence.DatabaseContext;

#nullable disable

namespace MedicalAssistant.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MedicalAssistantDatabaseContext))]
    [Migration("20260925120000_AddChatRequests")]
    public partial class AddChatRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DoctorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: true),
                    ConsultationId = table.Column<int>(type: "integer", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ContextJson = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: true),
                    CitationsJson = table.Column<string>(type: "jsonb", nullable: true),
                    SuggestedActionsJson = table.Column<string>(type: "jsonb", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatRequests_CorrelationId",
                table: "ChatRequests",
                column: "CorrelationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ChatRequests");
        }
    }
}
