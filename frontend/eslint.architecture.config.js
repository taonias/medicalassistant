import tseslint from 'typescript-eslint'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import { defineConfig, globalIgnores } from 'eslint/config'
import { moduleBoundaryRules, outsideModulesBoundaryRule } from './eslint.module-boundaries.js'

// Runs only the module-boundary check (R38) — no React/hooks/refresh/TS
// rulesets — so this stays green regardless of unrelated lint findings (see
// K25 in docs/known-issues/refactor-baseline.md) and can be a real CI gate
// for import-boundary enforcement on its own, independent of `npm run lint`'s
// full ruleset. `npm run lint` still runs this same boundary check too — this
// config exists so CI can check it in isolation.
//
// The TS parser is still needed here (just not typescript-eslint's rule
// set) — no-restricted-imports has to parse .ts/.tsx syntax to see the
// import statements at all. react-hooks/react-refresh are registered as
// plugins (not enabled as rules) purely so an existing
// `eslint-disable-next-line react-hooks/...` comment elsewhere in a file
// still names a rule ESLint recognizes — otherwise the unknown-rule-name
// check itself fails, unrelated to any actual import boundary.
export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: { parser: tseslint.parser },
    plugins: { 'react-hooks': reactHooks, 'react-refresh': reactRefresh },
  },
  ...moduleBoundaryRules,
  outsideModulesBoundaryRule,
])
