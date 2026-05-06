---
name: unit-tests-frontend
description: "Write minimal Vitest + Vue Test Utils tests for the unified-scheduling web frontend. Use when adding tests for Vue components, composables, stores, or utilities in web/src/. Covers test layout, setup helpers, mocking patterns, and run commands."
---

# Frontend Unit Tests — Vue 3 / Vitest

## Scope
Applies to files under `web/src/` and tests under `web/src/__tests__/`.

## Project Layout
```
web/
  vitest.config.ts          ← test config (happy-dom, globals, setupFiles)
  src/
    __tests__/
      helpers/
        setup.ts            ← global test setup (mount helpers, Vuetify, pinia)
      mocks/                ← shared stub/mock factories
      composables/          ← composable-level tests
      modules/              ← feature module tests
      App.spec.ts
```

## Procedure

1. **Identify changed behavior** — read the modified `.vue`, composable, or store file and note props, emits, reactive state, and side effects.
2. **Find the nearest test file** — look under `web/src/__tests__/` for an existing spec in the same folder or module. Mirror its mount helpers, mock imports, and assertion style.
3. **Write minimal tests** — cover:
   - Happy path rendering / default state
   - Key user interactions (clicks, input events) that change observable state
   - Error / empty states if the component handles them explicitly
4. **Run tests** — use the scoped command below; do **not** run Playwright e2e unless asked.
5. **Report** results using the Output Format.

## Naming Convention
```
describe('<ComponentName or composable>', () => {
  it('does X when Y', () => { … })
})
// e.g. it('shows error message when API call fails')
```

## Run Commands
```bash
# Run all unit tests (excludes e2e/)
cd web && npm run test:unit

# Watch mode during development
cd web && npx vitest --watch

# Run a single file
cd web && npx vitest src/__tests__/modules/MyFeature.spec.ts
```

## Conventions
- Use `vitest` globals (`describe`, `it`, `expect`, `vi`) — they are enabled in `vitest.config.ts`.
- Mount components with the helpers in `web/src/__tests__/helpers/setup.ts`; do **not** create a fresh Vuetify/Pinia setup inline.
- **Do NOT use `vi.mock()` for network calls.** The project uses MSW (Mock Service Worker) — call `api-access/` functions directly in tests; MSW intercepts them. Define or reuse handlers in `web/src/__tests__/mocks/`.
- Use `vi.fn()` only for non-network side effects (e.g., router navigation, browser APIs).
- Do **not** use `setTimeout` / real timers; use `vi.useFakeTimers()` if time-dependent behavior must be tested.
- Keep DOM queries readable: prefer `getByRole` / `getByText` patterns from `@testing-library/vue` if already used nearby; otherwise use `wrapper.find`.
