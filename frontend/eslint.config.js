import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import boundaries from 'eslint-plugin-boundaries'
import { defineConfig, globalIgnores } from 'eslint/config'

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
  {
    // Each feature's public surface is its index.ts (R11). A feature may use
    // its own internals freely; anything outside it must go through the
    // barrel, so a folder or file can move without hunting down every caller.
    files: ['src/**/*.{ts,tsx}'],
    plugins: { boundaries },
    settings: {
      'boundaries/elements': [
        { type: 'feature', pattern: 'src/features/*' },
        { type: 'app', pattern: 'src/app' },
        { type: 'layouts', pattern: 'src/layouts' },
        { type: 'shared', pattern: 'src/shared' },
      ],
    },
    rules: {
      // entry-point is deprecated in favor of `dependencies` + selectors (v7), but
      // still supported; revisit at the next plugin major version.
      'boundaries/entry-point': [
        'error',
        {
          default: 'disallow',
          policies: [{ target: { element: { type: 'feature' } }, allow: 'index.ts' }],
        },
      ],
    },
  },
])
