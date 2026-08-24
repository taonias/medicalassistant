import { describe, expect, it } from 'vitest';

import { authKeys } from '../../features/auth';
import { patientKeys } from '../../features/patients';
import { consultationKeys } from '../../modules/consultations';
import { transcriptKeys, structuredDataKeys, doctorNotesKeys } from '../../modules/clinical-record';
import { conversationKeys, actionKeys } from '../../modules/chat';

describe('React Query key contract', () => {
  it('keeps the complete cache-key catalog stable for feature owners', () => {
    expect({
      session: authKeys.session,
      patient: patientKeys.patient(7),
      patients: patientKeys.patients,
      patientHistory: patientKeys.patientHistory(7),
      patientHistoryPrefix: patientKeys.patientHistoryPrefix(7),
      consultationsByPatient: consultationKeys.consultationsByPatient(7),
      draftConsultations: consultationKeys.draftConsultations,
      unattachedDraftConsultations: consultationKeys.unattachedDraftConsultations,
      dashboardAnalytics: consultationKeys.dashboardAnalytics,
      consultation: consultationKeys.consultation(42),
      consultationAudio: consultationKeys.consultationAudio(42),
      transcript: transcriptKeys.transcript(42),
      structuredData: structuredDataKeys.structuredData(42),
      doctorNotes: doctorNotesKeys.doctorNotes(42),
      patientDoctorNotes: doctorNotesKeys.patientDoctorNotes(7),
      action: actionKeys.action('correlation-1'),
      conversations: conversationKeys.conversations(7),
      conversationThread: conversationKeys.conversationThread(5),
    }).toEqual({
      session: ['auth', 'session'],
      patient: ['patient', 7],
      patients: ['patients'],
      patientHistory: ['patient', 7, 'history', null, null, null, null, null],
      patientHistoryPrefix: ['patient', 7, 'history'],
      consultationsByPatient: ['patient', 7, 'consultations'],
      draftConsultations: ['consultations', 'drafts'],
      unattachedDraftConsultations: ['consultations', 'drafts', 'unattached'],
      dashboardAnalytics: ['consultations', 'analytics'],
      consultation: ['consultation', 42],
      consultationAudio: ['consultation', 42, 'audio'],
      transcript: ['consultation', 42, 'transcript'],
      structuredData: ['consultation', 42, 'structured-data'],
      doctorNotes: ['consultation', 42, 'doctor-notes'],
      patientDoctorNotes: ['patient', 7, 'doctor-notes'],
      action: ['action', 'correlation-1'],
      conversations: ['patient', 7, 'conversations'],
      conversationThread: ['conversation', 5, 'thread'],
    });
  });

  it('keeps Patient history filters in a stable, explicit cache key', () => {
    expect(patientKeys.patientHistory(7, '2026-08-01', '2026-08-23', 2, 25, 'Audio')).toEqual([
      'patient',
      7,
      'history',
      '2026-08-01',
      '2026-08-23',
      2,
      25,
      'Audio',
    ]);
    expect(patientKeys.patientHistoryPrefix(7)).toEqual(['patient', 7, 'history']);
  });

  it('keeps Consultation-owned cache keys distinct by capability', () => {
    expect({
      consultation: consultationKeys.consultation(42),
      audio: consultationKeys.consultationAudio(42),
      transcript: transcriptKeys.transcript(42),
      structuredData: structuredDataKeys.structuredData(42),
      doctorNotes: doctorNotesKeys.doctorNotes(42),
    }).toEqual({
      consultation: ['consultation', 42],
      audio: ['consultation', 42, 'audio'],
      transcript: ['consultation', 42, 'transcript'],
      structuredData: ['consultation', 42, 'structured-data'],
      doctorNotes: ['consultation', 42, 'doctor-notes'],
    });
  });
});
