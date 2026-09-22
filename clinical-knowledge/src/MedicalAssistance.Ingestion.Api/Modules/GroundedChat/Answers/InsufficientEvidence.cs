namespace MedicalAssistance.Ingestion.Api.GroundedChat;

/// <summary>
/// The insufficient-evidence refusal text — deterministic and code-owned, selected
/// by the question's language (ADR-0012). No model is called on the refusal path: an
/// honest "the record does not support an answer" must never itself be generated
/// prose that could drift or hallucinate. It is a plain, fixed sentence per language.
/// </summary>
internal static class InsufficientEvidence
{
    /// <summary>
    /// The exact, literal reply the GroundedChat agent's instructions tell it to give
    /// when the supplied evidence (which did clear the retrieval threshold) still
    /// isn't enough to answer. Retrieval finding *something* doesn't mean it found the
    /// *right* thing, so this is a second, model-detected insufficiency case distinct
    /// from the zero-evidence one above — but it still needs the same deterministic
    /// answer text and zero citations, not whatever free-form refusal prose the model
    /// would otherwise write while still citing the [E#] evidence it considered and
    /// rejected. Keep this in sync with the "GroundedChat" row in
    /// agent_instructions (see the SeedGroundedChatAgent /
    /// UpdateGroundedChatInsufficientEvidenceInstructions migrations) — the model has
    /// to be told to emit exactly this token.
    /// </summary>
    public const string Sentinel = "INSUFFICIENT_EVIDENCE";

    /// <summary>True if the generated answer is (modulo whitespace/case/trailing punctuation) just <see cref="Sentinel"/>.</summary>
    public static bool IsSentinel(string answer) =>
        answer.Trim().TrimEnd('.', '!').Equals(Sentinel, StringComparison.OrdinalIgnoreCase);

    /// <summary>The refusal sentence for the given language (el/en), defaulting to English.</summary>
    public static string Message(string language) => language switch
    {
        "el" => "Δεν υπάρχουν επαρκή στοιχεία στον φάκελο του ασθενή για να απαντηθεί αυτή η ερώτηση.",
        _ => "There is not enough evidence in the patient's record to answer this question.",
    };
}
