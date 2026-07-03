import { Navigate, useParams, useSearchParams } from 'react-router-dom';
import { ChatPage } from '../../features/chat/pages/ChatPage';

export function PatientChatRoute() {
  const { patientId = '0' } = useParams();
  const [searchParams] = useSearchParams();
  const consultationId = searchParams.get('consultation');
  const id = Number(patientId);

  if (!id) return <Navigate to="/patients" replace />;

  return (
    <ChatPage
      patientId={id}
      consultationId={consultationId ? Number(consultationId) : undefined}
    />
  );
}
