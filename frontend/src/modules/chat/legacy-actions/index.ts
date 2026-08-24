// Public surface of the legacy-actions feature: the AI action-trigger path
// that pre-dates conversations. Quarantined here rather than deleted — no
// page wires it up today, but its contract stays characterized (see R20).
export { useTriggerAction, useActionStatus } from './hooks/useActions';
export { ActionConfirmationModal } from './components/ActionConfirmationModal';
export * from './types';
export { actionKeys } from './queryKeys';
