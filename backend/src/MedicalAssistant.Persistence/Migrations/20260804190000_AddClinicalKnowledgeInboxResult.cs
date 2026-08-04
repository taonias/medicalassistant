using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalAssistant.Persistence.Migrations
{
    public partial class AddClinicalKnowledgeInboxResult : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClinicalKnowledgeIngestionId",
                table: "ConsultationInboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClinicalKnowledgeDocumentId",
                table: "ConsultationInboxMessages",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ClinicalKnowledgeDuplicate",
                table: "ConsultationInboxMessages",
                type: "boolean",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClinicalKnowledgeIngestionId",
                table: "ConsultationInboxMessages");

            migrationBuilder.DropColumn(
                name: "ClinicalKnowledgeDocumentId",
                table: "ConsultationInboxMessages");

            migrationBuilder.DropColumn(
                name: "ClinicalKnowledgeDuplicate",
                table: "ConsultationInboxMessages");
        }
    }
}
