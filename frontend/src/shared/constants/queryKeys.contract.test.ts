import { describe, expect, it } from 'vitest';

import { queryKeys } from './queryKeys';

describe('React Query key contract', () => {
  it('keeps the complete cache-key catalog stable for feature owners', () => {
    expect({
      session: queryKeys.session,
      patient: queryKeys.patient(7),
      patients: queryKeys.patients,
      patientHistory: queryKeys.patientHistory(7),
      patientHistoryPrefix: queryKeys.patientHistoryPrefix(7),
      consultationsByPatient: queryKeys.consultationsByPatient(7),
      draftConsultations: queryKeys.draftConsultations,
      unattachedDraftConsultations: queryKeys.unattachedDraftConsultations,
      dashboardAnalytics: queryKeys.dashboardAnalytics,
      consultation: queryKeys.consultation(42),
      consultationAudio: queryKeys.consultationAudio(42),
      transcript: queryKeys.transcript(42),
      structuredData: queryKeys.structuredData(42),
      doctorNotes: queryKeys.doctorNotes(42),
      patientDoctorNotes: queryKeys.patientDoctorNotes(7),
      action: queryKeys.action('correlation-1'),
      conversations: queryKeys.conversations(7),
      conversationThread: queryKeys.conversationThread(5),
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
    expect(queryKeys.patientHistory(7, '2026-08-01', '2026-08-23', 2, 25, 'Audio')).toEqual([
      'patient',
      7,
      'history',
      '2026-08-01',
      '2026-08-23',
      2,
      25,
      'Audio',
    ]);
    expect(queryKeys.patientHistoryPrefix(7)).toEqual(['patient', 7, 'history']);
  });

  it('keeps Consultation-owned cache keys distinct by capability', () => {
    expect({
      consultation: queryKeys.consultation(42),
      audio: queryKeys.consultationAudio(42),
      transcript: queryKeys.transcript(42),
      structuredData: queryKeys.structuredData(42),
      doctorNotes: queryKeys.doctorNotes(42),
    }).toEqual({
      consultation: ['consultation', 42],
      audio: ['consultation', 42, 'audio'],
      transcript: ['consultation', 42, 'transcript'],
      structuredData: ['consultation', 42, 'structured-data'],
      doctorNotes: ['consultation', 42, 'doctor-notes'],
    });
  });
});
