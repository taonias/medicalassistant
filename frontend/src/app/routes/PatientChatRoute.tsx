import { Navigate, useParams } from 'react-router-dom';
import { ChatPage } from '../../features/chat/pages/ChatPage';

export function PatientChatRoute() {
  const { patientId = '0' } = useParams();
  const id = Number(patientId);

  if (!id) return <Navigate to="/patients" replace />;

  return <ChatPage patientId={id} />;
}
