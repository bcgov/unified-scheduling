---
name: bc-design-tokens
description: "Explain BC Gov design tokens, their naming convention, and how to apply them. Use when asked about design tokens, CSS variables, token naming, or how to map BC Gov values to a framework theme (e.g. Vuetify)."
---

# BC Gov Design Tokens

1. Explain the naming convention: `System.Usage.Property.Modifier.Component.Variant`
2. Show the relevant token table(s) from the agent's cached knowledge:
   - For colour tokens → present the full colour tables (invoke `bc-design-colours`)
   - For spacing/layout tokens → present the margin, padding, border radius, border width, and icon size tables
3. Explain the benefit: token aliases replace hard-coded values so future design changes only require updating the token definition, not every usage in code
4. Reference the official docs: https://www2.gov.bc.ca/gov/content/digital/design-system/foundations/design-tokens

## Mapping BC Gov tokens to this project's Vuetify theme

| BC Gov token / value     | Vuetify CSS variable                  |
| ------------------------ | ------------------------------------- |
| `#013366` (primaryBlue)  | `rgb(var(--v-theme-primary))`         |
| `#FCBA19` (primaryGold)  | `rgb(var(--v-theme-accent))`          |
| `#2E8540` (success)      | `rgb(var(--v-theme-success))`         |
| `#D8292F` (danger/error) | `rgb(var(--v-theme-error))`           |
| `#F5A623` (warning)      | `rgb(var(--v-theme-warning))`         |
| `#1A5A96` (info)         | `rgb(var(--v-theme-info))`            |
| `#FFFFFF` (surface)      | `rgb(var(--v-theme-surface))`         |
| Spacing                  | `--ua-spacing-*` from `variables.css` |
