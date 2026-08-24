export interface MedicalStructuredDataDto {
  id: number;
  consultationId: number;
  transcriptId?: number | null;
  schemaVersion: string;
  structuredPayload: string;
  extractedAt: string;
  approved: boolean;
}

export interface StructuredDataSection {
  type: string;
  label: string;
  fields: StructuredDataField[];
}

export interface StructuredDataField {
  key: string;
  label: string;
  value: string | number | boolean | null;
  confidence?: number;
  source?: string;
}

export interface ParsedStructuredSummary {
  summary?: string;
  sections: StructuredDataSection[];
}
