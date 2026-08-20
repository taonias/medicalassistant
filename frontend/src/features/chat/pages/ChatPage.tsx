import { ConversationChat } from '../components/ConversationChat';
import { LegacyGeneralChat } from '../components/LegacyGeneralChat';

interface Props {
  patientId?: number;
  consultationId?: number;
}

/**
 * Patient-scoped chat gets the full stateful conversation experience (history, live
 * progress, interactive citations, retry). The general (no-patient) chat keeps the
 * legacy stateless single-shot flow.
 */
export function ChatPage({ patientId, consultationId }: Props) {
  if (patientId === undefined) {
    return <LegacyGeneralChat />;
  }

  return <ConversationChat patientId={patientId} consultationId={consultationId} />;
}
