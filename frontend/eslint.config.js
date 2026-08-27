import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'
import { moduleBoundaryRules, outsideModulesBoundaryRule } from './eslint.module-boundaries.js'

// Module-boundary rules (which capability may import which) live in
// eslint.module-boundaries.js (R38) so they can also run standalone via
// eslint.architecture.config.js — see that file's own comments for why.

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
  ...moduleBoundaryRules,
  outsideModulesBoundaryRule,
])
