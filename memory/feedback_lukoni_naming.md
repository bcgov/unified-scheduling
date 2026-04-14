---
name: BCSS Stats naming
description: Do not use "Lukoni" as a name in code — it refers to the legacy system. Use Stats/StatEntry naming instead.
type: feedback
---

Do not name files, classes, or variables after "Lukoni" — it is the name of the old legacy system and should not appear in code.

**Why:** The module is the Stats module within Unified Scheduling. Lukoni is a legacy system reference, not a concept that belongs in the new codebase.

**How to apply:** Use `Stats`, `StatEntry`, `StatsModule` etc. for all naming within the BCSS stats scaffold.
