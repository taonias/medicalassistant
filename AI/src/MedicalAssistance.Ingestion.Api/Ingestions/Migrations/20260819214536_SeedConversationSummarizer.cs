using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalAssistance.Ingestion.Api.Ingestions.Migrations
{
    /// <inheritdoc />
    public partial class SeedConversationSummarizer : Migration
    {
        // A fixed timestamp, not DateTimeOffset.UtcNow: a migration must produce the
        // same DML every time it is generated (mirrors SeedAgentInstructions).
        private static readonly DateTimeOffset SeededAt = new(2026, 8, 19, 0, 0, 0, TimeSpan.Zero);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The conversation-summarizer agent (ADR-0008): folds the older turns of a
            // doctor↔AI chat into one rolling summary the backend keeps, so follow-up
            // questions retain their referents once earlier turns scroll out of the
            // verbatim window. The summary is phrasing/refinement context only — never
            // evidence. DB-owned like every other agent, tuned by editing this row.
            migrationBuilder.InsertData(
                table: "agent_instructions",
                columns: ["name", "instructions", "version", "updated_at"],
                values: new object[]
                {
                    "ConversationSummarizer",
                    "You maintain a rolling summary of a conversation between a doctor and a clinical " +
                    "assistant about one patient. You are given the existing summary (may be empty) and the " +
                    "next batch of turns to fold in, oldest first.\n\n" +
                    "Rules:\n" +
                    "- Produce ONE updated summary that combines the existing summary with the new turns. Do not " +
                    "answer the questions or add outside knowledge.\n" +
                    "- Preserve referents a later question may depend on: which patient attributes, conditions, " +
                    "medications, dates, and prior answers were discussed, and any decisions or open threads.\n" +
                    "- Be concise and factual — a few short sentences or compact bullet points. Drop pleasantries " +
                    "and redundancy; keep specifics.\n" +
                    "- Write in the language the conversation is conducted in.\n" +
                    "- Respond with the updated summary text only — no preamble and no headings.",
                    1,
                    SeededAt,
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "agent_instructions", keyColumn: "name", keyValue: "ConversationSummarizer");
        }
    }
}
