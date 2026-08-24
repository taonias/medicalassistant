import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

// Every folder directly under src/features is a feature; each one's public
// surface is its index.ts (R11). Code outside a feature — another feature,
// the app shell, layouts, or shared — must import it only through that
// barrel, never by a path that reaches into the feature's internals. A file
// can then move within its own feature without hunting down every caller.
const featureNames = [
  'audio-capture',
  'auth',
  'chat',
  'consultations',
  'dashboard',
  'doctor-notes',
  'medical-data',
  'patients',
  'record',
  'settings',
  'theme',
  'transcripts',
]

// Matches "…/<name>/<anything>" at any relative depth, but not the bare
// barrel import "…/<name>" itself (no trailing path segment).
function internalPathPattern(featureName) {
  return {
    group: [`**/${featureName}/**`],
    message: `Import "${featureName}" from its public index (features/${featureName}), not by reaching into its internals.`,
  }
}

// ESLint flat config merges same-named rules across matching blocks by full
// overwrite, not by combining arrays — so each file must match exactly one
// of these blocks, each carrying the complete pattern set that applies to it
// (every *other* feature's internals), rather than one block per feature all
// matching the same files and clobbering each other's patterns.
const featureBoundaryRules = featureNames.map((ownFeature) => ({
  files: [`src/features/${ownFeature}/**/*.{ts,tsx}`],
  rules: {
    'no-restricted-imports': [
      'error',
      {
        patterns: featureNames
          .filter((name) => name !== ownFeature)
          .map(internalPathPattern),
      },
    ],
  },
}))

const outsideFeaturesBoundaryRule = {
  files: ['src/**/*.{ts,tsx}'],
  ignores: ['src/features/**'],
  rules: {
    'no-restricted-imports': ['error', { patterns: featureNames.map(internalPathPattern) }],
  },
}

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      globals: globals.browser,
    },
  },
  ...featureBoundaryRules,
  outsideFeaturesBoundaryRule,
])
