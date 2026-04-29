---
name: bc-design-create-component
description: "Create a new shared Vue component aligned to the BC Government Design System. Use when asked to build, scaffold, or add a new shared UI component following BC Gov Visual Identity."
---

# Create a New Shared Component

1. Read `.github/copilot-instructions.md` for the project's tech stack and conventions
2. Identify the closest BC Gov Design System component (invoke `bc-design-components` if needed)
3. Map BC Gov design tokens to Vuetify theme values using the `bc-design-tokens` skill mapping table
4. Create the component as a Vue 3 `<script setup lang="ts">` SFC in `web/src/shared/components/` following the `Ua` prefix naming convention
5. Do **not** import React components — reimplement the design intent only
6. Use scoped styles with `--v-theme-*` and `--ua-*` CSS variables; no hardcoded hex values
7. Add a test in `web/src/__tests__/shared/components/UaComponents.spec.ts`
8. Verify WCAG AA compliance using the `bc-design-wcag` skill:
   - Visible focus ring on all interactive elements
   - `aria-label` or associated `<label>` for all controls
   - Errors communicated in text, not colour alone
   - Colour contrast meets minimums with the theme values

## Component file template

```vue
<script setup lang="ts">
// props, emits, logic
</script>

<template>
  <!-- semantic HTML, aria attributes -->
</template>

<style scoped>
/* use --v-theme-* and --ua-* variables — no hardcoded hex values */
</style>
```
