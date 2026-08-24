// Public surface of the auth module's route-guard UI (R13). Auth state and
// screens still live in features/auth (see its own index.ts) until R19/R20
// consolidate features/ into modules/ wholesale; this module holds only the
// pieces R13 explicitly relocated.
export { ProtectedRoute } from './ui/guards/ProtectedRoute';
export { RequirePermission } from './ui/guards/RequirePermission';
