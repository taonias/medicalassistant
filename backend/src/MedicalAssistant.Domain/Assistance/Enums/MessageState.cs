namespace MedicalAssistant.Domain.Enums;

/// <summary>
/// The lifecycle of a single assistant turn. A user turn is always
/// <see cref="Completed"/>. A refusal (insufficient evidence) is a normal
/// answer, not a failure — it is <see cref="Refused"/>, never <see cref="Failed"/>.
/// </summary>
public enum MessageState
{
    Pending = 0,
    Completed = 1,
    Refused = 2,
    Failed = 3
}
