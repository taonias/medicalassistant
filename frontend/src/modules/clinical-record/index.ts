// Public surface of the clinical-record module (R19): the review material a
// doctor consults about a patient's care — transcripts, doctor notes, and
// structured medical data. Co-located here because ownership follows the
// capability a doctor uses, not the component type; each sub-area keeps its
// own internal structure (R19 moves files, it does not deduplicate them).
export * from './transcripts';
export * from './doctor-notes';
export * from './medical-data';
