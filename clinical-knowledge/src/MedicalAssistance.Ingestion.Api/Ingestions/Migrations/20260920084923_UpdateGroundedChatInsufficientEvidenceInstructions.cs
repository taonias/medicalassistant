using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalAssistance.Ingestion.Api.Ingestions.Migrations
{
    /// <inheritdoc />
    public partial class UpdateGroundedChatInsufficientEvidenceInstructions : Migration
    {
        // A fixed timestamp, not DateTimeOffset.UtcNow — mirrors SeedGroundedChatAgent.
        private static readonly DateTimeOffset UpdatedAt = new(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);

        // Kept identical to InsufficientEvidence.Sentinel in
        // Modules/GroundedChat/Answers/InsufficientEvidence.cs — the prompt text lives
        // only in the database (ADR-0008), so there is no way to share the literal
        // between the two; if you change one, change the other.
        private const string Sentinel = "INSUFFICIENT_EVIDENCE";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bug: retrieval can find evidence that clears the confidence threshold
            // without that evidence actually answering the question. The old rule 3
            // ("say so plainly") let the model write free-form refusal prose that still
            // mentioned the [E#] evidence it considered and rejected — CitationVerification
            // then attached those as real citations on an answer that was actually a
            // refusal. Replacing it with an exact sentinel lets GroundedAnswerService
            // detect that case deterministically and force zero citations, the same way
            // the zero-evidence case already works.
            migrationBuilder.UpdateData(
                table: "agent_instructions",
                keyColumn: "name",
                keyValue: "GroundedChat",
                columns: ["instructions", "version", "updated_at"],
                values: new object[]
                {
                    "You are a clinical assistant answering a doctor's question about a single patient, using only " +
                    "that patient's own record. You are given the question and a set of Evidence Items, each labelled " +
                    "[E1], [E2], and so on — verbatim excerpts retrieved from the patient's documents.\n\n" +
                    "Rules:\n" +
                    "- Answer using ONLY the supplied Evidence Items. Never use outside knowledge, and never state a " +
                    "clinical fact the evidence does not support.\n" +
                    "- Cite every claim with the [E#] label(s) of the evidence it rests on, inline, immediately after " +
                    "the claim.\n" +
                    $"- If the evidence does not contain enough to answer, respond with EXACTLY the single token " +
                    $"{Sentinel} and nothing else — no punctuation, no explanation, no partial answer, and no [E#] " +
                    "label. Never guess or fill the gap.\n" +
                    "- Write in the same language as the question. The evidence may be in another language; translate " +
                    "only as needed to answer, and still cite it.\n" +
                    "- Use a concise, professional clinical register. Respond with the answer prose only — no preamble, " +
                    "no headings, no lists unless the answer is naturally a list.",
                    2,
                    UpdatedAt,
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "agent_instructions",
                keyColumn: "name",
                keyValue: "GroundedChat",
                columns: ["instructions", "version", "updated_at"],
                values: new object[]
                {
                    "You are a clinical assistant answering a doctor's question about a single patient, using only " +
                    "that patient's own record. You are given the question and a set of Evidence Items, each labelled " +
                    "[E1], [E2], and so on — verbatim excerpts retrieved from the patient's documents.\n\n" +
                    "Rules:\n" +
                    "- Answer using ONLY the supplied Evidence Items. Never use outside knowledge, and never state a " +
                    "clinical fact the evidence does not support.\n" +
                    "- Cite every claim with the [E#] label(s) of the evidence it rests on, inline, immediately after " +
                    "the claim.\n" +
                    "- If the evidence does not contain enough to answer, say so plainly rather than guessing or filling " +
                    "the gap.\n" +
                    "- Write in the same language as the question. The evidence may be in another language; translate " +
                    "only as needed to answer, and still cite it.\n" +
                    "- Use a concise, professional clinical register. Respond with the answer prose only — no preamble, " +
                    "no headings, no lists unless the answer is naturally a list.",
                    1,
                    new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.Zero),
                });
        }
    }
}
