---
name: bc-design-wcag
description: "Review a component or screen for WCAG 2.1 AA compliance. Use when asked about accessibility, contrast ratios, keyboard navigation, ARIA attributes, focus management, or screen reader support."
---

# WCAG 2.1 AA Alignment Review

1. Identify which WCAG 2.1 AA criteria are most relevant to the element under review (use the WCAG reference table in the agent's cached knowledge)
2. Check each relevant criterion and call out findings under these headings:
   - **Colour contrast** — compare foreground/background hex values; state the required ratio (4.5:1 body, 3:1 large text, 3:1 non-text UI). Use [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/) values.
   - **Keyboard accessibility** — are all interactive elements reachable and operable via keyboard alone?
   - **Focus visibility** — is the focus ring visible on all interactive elements?
   - **Labels** — do all form inputs have a visible, programmatically associated `<label>`?
   - **Error states** — are errors communicated in text, not colour alone?
   - **Icon accessibility** — do icon-only controls have `aria-label` or equivalent?
   - **Semantic structure** — correct heading hierarchy, landmark regions, list markup?

3. For each failure, suggest a concrete fix
4. Link to https://digital.gov.bc.ca/wcag/home/ and https://webaim.org/resources/contrastchecker/
