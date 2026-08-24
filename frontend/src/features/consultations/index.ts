// Public surface of the consultations feature.
export {
  useConsultation,
  useAssignConsultationPatient,
  useConsultationAudio,
  useDashboardAnalytics,
  useUnattachedDraftConsultations,
  useCreateConsultation,
  useUploadConsultationAudio,
  useUploadConsultationDocument,
  useDeleteConsultation,
} from './hooks/useConsultations';
export { ConsultationDetailPage } from './pages/ConsultationDetailPage';
