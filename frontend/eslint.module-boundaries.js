// Every business capability's public surface is its index.ts (R11). Code
// outside a capability — another capability, the app shell, or shared — must
// import it only through that barrel, never by a path that reaches into its
// internals. A file can then move within its own capability without hunting
// down every caller.
//
// Most capabilities still live under features/<name> (pre-R19). R19 moved
// consultations (plus its record/audio-capture sub-areas) and clinical-record
// (transcripts/doctor-notes/medical-data) to modules/<name> instead; R13
// split auth's route guards into modules/auth while the rest of auth stays
// at features/auth until a later wave finishes the features/ -> modules/
// consolidation. R20 moved chat to modules/chat, split into its own
// conversations/legacy/legacy-actions sub-areas (same "each sub-area keeps
// its own internal structure" pattern as consultations/clinical-record) so a
// stray import can't quietly extend a legacy generation. `roots` names every
// glob a capability's own files can live under, so self-imports stay exempt
// regardless of which root they're in — the protection pattern itself is
// root-agnostic (matches by name only), so a capability split across roots
// is still protected as one unit.
//
// Extracted from eslint.config.js (R38) so this — the one part of frontend
// linting that is actual architecture enforcement, not code-quality style —
// can be run on its own in CI via eslint.architecture.config.js, independent
// of unrelated lint findings (see K25 in docs/known-issues/refactor-baseline.md).
export const modules = [
  { name: 'auth', roots: ['src/features/auth/**/*.{ts,tsx}', 'src/modules/auth/**/*.{ts,tsx}'] },
  { name: 'chat', roots: ['src/modules/chat/**/*.{ts,tsx}'] },
  { name: 'dashboard', roots: ['src/features/dashboard/**/*.{ts,tsx}'] },
  { name: 'patients', roots: ['src/features/patients/**/*.{ts,tsx}'] },
  { name: 'settings', roots: ['src/features/settings/**/*.{ts,tsx}'] },
  { name: 'theme', roots: ['src/features/theme/**/*.{ts,tsx}'] },
  { name: 'consultations', roots: ['src/modules/consultations/**/*.{ts,tsx}'] },
  { name: 'clinical-record', roots: ['src/modules/clinical-record/**/*.{ts,tsx}'] },
]

// Matches "…/<name>/<anything>" at any relative depth, but not the bare
// barrel import "…/<name>" itself (no trailing path segment).
function internalPathPattern(name) {
  return {
    group: [`**/${name}/**`],
    message: `Import "${name}" from its public index, not by reaching into its internals.`,
  }
}

// ESLint flat config merges same-named rules across matching blocks by full
// overwrite, not by combining arrays — so each file must match exactly one
// of these blocks, each carrying the complete pattern set that applies to it
// (every *other* module's internals), rather than one block per module all
// matching the same files and clobbering each other's patterns.
export const moduleBoundaryRules = modules.map((ownModule) => ({
  files: ownModule.roots,
  rules: {
    'no-restricted-imports': [
      'error',
      {
        patterns: modules
          .filter((m) => m.name !== ownModule.name)
          .map((m) => internalPathPattern(m.name)),
      },
    ],
  },
}))

export const outsideModulesBoundaryRule = {
  files: ['src/**/*.{ts,tsx}'],
  ignores: modules.flatMap((m) => m.roots),
  rules: {
    'no-restricted-imports': ['error', { patterns: modules.map((m) => internalPathPattern(m.name)) }],
  },
}
