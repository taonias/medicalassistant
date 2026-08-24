export interface Transcript {
  id: number;
  consultationId: number;
  status: string;
  transcript?: string;
  externalJobId?: string;
  processedAt?: string;
  failureReason?: string;
}
