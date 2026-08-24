import { createBrowserRouter, Navigate } from 'react-router-dom';
import { ProtectedRoute } from '../modules/auth';
import { LoginPage } from '../features/auth';
import { DashboardPage } from '../features/dashboard';
import { NewConsultationPage } from '../features/audio-capture';
import { ConsultationDetailPage } from '../features/consultations';
import {
  PatientDetailPage,
  PatientListPage,
  PatientConsultationsTab,
  PatientOverviewTab,
} from '../features/patients';
import { AppShell } from './shell/AppShell';
import { AuthLayout } from './shell/AuthLayout';
import { ChatPage } from '../features/chat';
import { RecordPage } from '../features/record';
import { SettingsPage } from '../features/settings';
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
              { path: 'history', element: <PatientConsultationsTab /> },
              { path: 'structured-data', element: <Navigate to=".." replace /> },
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
