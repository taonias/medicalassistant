import { createBrowserRouter, Navigate } from 'react-router-dom';
import { ProtectedRoute } from '../shared/components/ProtectedRoute';
import { LoginPage } from '../features/auth/pages/LoginPage';
import { DashboardPage } from '../features/dashboard/pages/DashboardPage';
import { NewConsultationPage } from '../features/audio-capture/pages/NewConsultationPage';
import { ConsultationDetailPage } from '../features/consultations/pages/ConsultationDetailPage';
import { PatientDetailPage } from '../features/patients/pages/PatientDetailPage';
import { PatientListPage } from '../features/patients/pages/PatientListPage';
import {
  PatientHistoryTab,
  PatientOverviewTab,
  PatientStructuredDataTab,
} from '../features/patients/pages/PatientTabs';
import { AppShell } from '../layouts/AppShell';
import { AuthLayout } from '../layouts/AuthLayout';
import { ChatPage } from '../features/chat/pages/ChatPage';
import { RecordPage } from '../features/record/pages/RecordPage';
import { SettingsPage } from '../features/settings/pages/SettingsPage';
import { PatientChatRoute } from './routes/PatientChatRoute';

export const router = createBrowserRouter([
  {
    element: <AuthLayout />,
    children: [{ path: '/login', element: <LoginPage /> }],
  },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppShell />,
        children: [
          { index: true, element: <DashboardPage /> },
          { path: 'record', element: <RecordPage /> },
          { path: 'chat', element: <ChatPage /> },
          { path: 'settings', element: <SettingsPage /> },
          { path: 'patients', element: <PatientListPage /> },
          {
            path: 'patients/:patientId',
            element: <PatientDetailPage />,
            children: [
              { index: true, element: <PatientOverviewTab /> },
              { path: 'history', element: <PatientHistoryTab /> },
              { path: 'structured-data', element: <PatientStructuredDataTab /> },
            ],
          },
          { path: 'patients/:patientId/consultations/new', element: <NewConsultationPage /> },
          {
            path: 'patients/:patientId/consultations/:consultationId',
            element: <ConsultationDetailPage />,
          },
          { path: 'consultations/:consultationId', element: <ConsultationDetailPage /> },
          { path: 'patients/:patientId/chat', element: <PatientChatRoute /> },
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
]);
