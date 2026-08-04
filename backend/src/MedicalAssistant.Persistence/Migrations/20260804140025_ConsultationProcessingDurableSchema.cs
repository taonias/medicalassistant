using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MedicalAssistant.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConsultationProcessingDurableSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "Transcripts",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Transcripts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Consultations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Consultations",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReasonCode",
                table: "Consultations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceFileKind",
                table: "Consultations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceObjectETag",
                table: "Consultations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceObjectReference",
                table: "Consultations",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConsultationDeletionCleanups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConsultationId = table.Column<int>(type: "integer", nullable: false),
                    DeletionEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BlobCleanupStatus = table.Column<int>(type: "integer", nullable: false),
                    ClinicalKnowledgeCleanupStatus = table.Column<int>(type: "integer", nullable: false),
                    BlobCleanupCompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClinicalKnowledgeCleanupCompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastFailureCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastFailureCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationDeletionCleanups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsultationDeletionCleanups_Consultations_ConsultationId",
                        column: x => x.ConsultationId,
                        principalTable: "Consultations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsultationInboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConsumerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventVersion = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseOwner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFailureCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastFailureCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationInboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsultationOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventVersion = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Producer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CausationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AggregateType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AggregateId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseOwner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFailureCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastFailureCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Consultations_DeletedAtUtc",
                table: "Consultations",
                column: "DeletedAtUtc",
                filter: "\"DeletedAtUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Consultations_SourceObjectReference",
                table: "Consultations",
                column: "SourceObjectReference",
                filter: "\"SourceObjectReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationDeletionCleanups_ConsultationId",
                table: "ConsultationDeletionCleanups",
                column: "ConsultationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationDeletionCleanups_DeletionEventId",
                table: "ConsultationDeletionCleanups",
                column: "DeletionEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationInboxMessages_ConsumerName_EventId",
                table: "ConsultationInboxMessages",
                columns: new[] { "ConsumerName", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationInboxMessages_ConsumerName_Status_LeaseExpiresA~",
                table: "ConsultationInboxMessages",
                columns: new[] { "ConsumerName", "Status", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationOutboxMessages_EventId",
                table: "ConsultationOutboxMessages",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationOutboxMessages_EventType_EventVersion",
                table: "ConsultationOutboxMessages",
                columns: new[] { "EventType", "EventVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationOutboxMessages_Status_NextAttemptAtUtc_LeaseExp~",
                table: "ConsultationOutboxMessages",
                columns: new[] { "Status", "NextAttemptAtUtc", "LeaseExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsultationDeletionCleanups");

            migrationBuilder.DropTable(
                name: "ConsultationInboxMessages");

            migrationBuilder.DropTable(
                name: "ConsultationOutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_Consultations_DeletedAtUtc",
                table: "Consultations");

            migrationBuilder.DropIndex(
                name: "IX_Consultations_SourceObjectReference",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "Transcripts");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Transcripts");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "DeletionReasonCode",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "SourceFileKind",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "SourceObjectETag",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "SourceObjectReference",
                table: "Consultations");
        }
    }
}
