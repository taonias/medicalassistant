// Legacy AI action trigger (pre-dates stateful conversations; see R20). Kept
// intact and quarantined here rather than deleted — no page wires it up
// today, but its contract is still characterized by chatApis.contract.test.ts.

export const ActionType = {
  SummarizeConsultation: 0,
  ExtractStructuredData: 1,
  ChatInsight: 2,
  CustomWorkflow: 3,
} as const;

export type ActionType = (typeof ActionType)[keyof typeof ActionType];

export interface TriggerActionRequest {
  actionType: ActionType;
  patientId?: number;
  consultationId?: number;
  parametersJson?: string;
  correlationId?: string;
}

export interface ActionRequest {
  id: number;
  correlationId: string;
  doctorId: string;
  patientId?: number;
  consultationId?: number;
  actionType: string;
  status: string;
  requestPayload?: string;
  responsePayload?: string;
  externalJobId?: string;
  failureReason?: string;
}
