using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MedicalAssistant.Persistence.DatabaseContext;

#nullable disable

namespace MedicalAssistant.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MedicalAssistantDatabaseContext))]
    [Migration("20260926130000_DropChatRequestConsultationId")]
    public partial class DropChatRequestConsultationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsultationId",
                table: "ChatRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsultationId",
                table: "ChatRequests",
                type: "integer",
                nullable: true);
        }
    }
}
