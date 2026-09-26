using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MedicalAssistant.Persistence.DatabaseContext;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MedicalAssistant.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MedicalAssistantDatabaseContext))]
    [Migration("20260926120000_DropActionRequests")]
    public partial class DropActionRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ActionRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DoctorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: true),
                    ConsultationId = table.Column<int>(type: "integer", nullable: true),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestPayload = table.Column<string>(type: "jsonb", nullable: true),
                    ResponsePayload = table.Column<string>(type: "jsonb", nullable: true),
                    ExternalJobId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionRequests_CorrelationId",
                table: "ActionRequests",
                column: "CorrelationId",
                unique: true);
        }
    }
}
