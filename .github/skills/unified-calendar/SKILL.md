---
name: unified-calendar
description: "Add or update Calendar module functionality using the contribution-based frontend architecture and Calendar-owned backend event model. Use when modifying calendar views, calendar data contributions, create/detail actions, or Calendar Event/EventSeries backend behavior."
argument-hint: "Describe the Calendar feature, view, contribution, action, or backend event change"
---

---

# Unified Calendar Module

## Purpose

Use this workflow when adding or modifying Calendar module behavior.

The Calendar module is a generic host for calendar display and calendar-owned events. It should remain independent of Scheduling, Training, or other domain modules unless those modules explicitly register their own calendar contributions.

The Calendar module supports extension through frontend registries and contribution objects, not by hardcoding module-specific behavior into the Calendar core.

## Core Principles

- Keep Calendar generic.
- Do not hardcode Scheduling, Training, Assignment, Shift, Team, or other domain-specific concepts in Calendar core.
- Calendar frontend views should project loaded contribution data into view models.
- Module contributions should own their own data loading.
- Calendar registries should remain plain TypeScript services/classes, not Pinia stores.
- Pinia should store Calendar UI state only.
- Backend Calendar persistence currently owns only Calendar `Event` and `EventSeries`.
- Calendar backend endpoints should not be view-specific.
- Calendar backend endpoints should not accept frontend `viewId` or module-specific identifiers to determine data loading.

## Backend Architecture

### Calendar-Owned Tables

The Calendar backend currently owns only:

- `EventSeries`
- `Event`

`EventSeries` represents a recurring event template/pattern.

`Event` represents a concrete event occurrence.

Calendar-owned events should use:

- `SourceModule = "calendar"`
- Calendar-owned event type codes such as:
  - `general`
  - `holiday`
  - `deadline`

Do not add Scheduling or Training persistence as part of Calendar module work.

### Backend Files

Expected backend areas include:

- `api/Unified.Calendar/CalendarModule.cs`
- `api/Unified.Calendar/Controllers/CalendarController.cs`
- `api/Unified.Calendar/Services/CalendarEventService.cs`
- `api/Unified.Calendar/Validators/CalendarEventsRequestValidator.cs`
- `api/Unified.Calendar/Models/Event.cs`
- `api/Unified.Calendar/Models/EventSeries.cs`
- EF configuration for `Event` and `EventSeries`
- API wiring in `Unified.Api` only through Calendar module registration

### Backend Rules

- Register Calendar backend services only when the Calendar feature flag is enabled, following existing project conventions.
- Make controller discovery explicit and consistent with Calendar service registration.
- Do not rely on accidental MVC controller discovery from referenced assemblies.
- Store concrete event instants in UTC.
- Store the IANA time zone separately, for example `America/Vancouver`.
- Do not rely on PostgreSQL `timestamptz` to preserve the original time zone.
- Use clear `[start, end)` overlap semantics for date-range event queries.
- Treat `EventSeries` as recurrence-ready schema; do not implement recurrence expansion unless specifically requested.

## Frontend Architecture

### Core Files

Expected frontend Calendar files include:

- `web/src/modules/calendar/Calendar.vue`
- `web/src/modules/calendar/calendarTypes.ts`
- `web/src/modules/calendar/calendarSelectors.ts`
- `web/src/modules/calendar/calendarDataService.ts`
- `web/src/modules/calendar/calendarStore.ts`
- `web/src/modules/calendar/calendarRoutes.ts`
- `web/src/modules/calendar/calendarNavigation.ts`
- `web/src/modules/calendar/CalendarModule.ts`
- `web/src/modules/calendar/registry/calendarRegistry.ts`
- `web/src/modules/calendar/registry/calendarActionRegistry.ts`
- `web/src/modules/calendar/contributions/calendarEventsContribution.ts`
- `web/src/modules/calendar/contributions/calendarEventMappers.ts`
- `web/src/modules/calendar/views/calendarDefaultViewContribution.ts`
- `web/src/modules/calendar/views/calendarWeekViewContribution.ts`
- `web/src/modules/calendar/views/calendarAgendaViewContribution.ts`
- `web/src/modules/calendar/actions/calendarCreateAction.ts`

Use existing project paths if they differ.

## Frontend Contribution Model

Calendar data is composed from registered module contributions.

The response shape is contribution-keyed:

```ts
export interface CalendarDataResponse {
  contributions: Record<string, CalendarContributionData>;
}
```

Each contribution owns its own data load:

```ts
export interface CalendarModuleContribution<
  TEvent extends CalendarEventBase = CalendarEventBase,
  TResource extends CalendarResourceBase = CalendarResourceBase,
  TData = unknown,
> {
  id: string;
  moduleId: string;
  contributionId: string;

  isAvailable?: (
    runtimeContext: CalendarRuntimeContext,
    queryContext: CalendarQueryContext,
  ) => boolean;

  load(
    queryContext: CalendarQueryContext,
    options?: { signal?: AbortSignal },
  ): Promise<CalendarContributionData<TEvent, TResource, TData>>;
}
```

The Calendar-owned contribution should use:

```text
moduleId = "calendar"
contributionId = "calendar.events"
```

Future modules should register their own contributions rather than modifying Calendar-owned loaders.

## Calendar Views

Calendar views are registered through `calendarRegistry`.

A view should:

- identify itself with a stable `id`
- expose a label/order/range as needed
- define a component
- build/project a view model from `CalendarDataResponse`
- avoid backend calls

Example concept:

```ts
calendarRegistry.registerView(calendarWeekViewContribution);
calendarRegistry.registerView(calendarAgendaViewContribution);
```

Views should not fetch data directly. Data loading belongs to `CalendarDataService` and registered module contributions.

### View Model Rule

Views should transform:

```text
CalendarDataResponse + Calendar UI state + runtime context
```

into a view-specific model.

They should not mutate the loaded response.

## CalendarDataService

`CalendarDataService` coordinates frontend data loading.

It should:

- build a `CalendarQueryContext` from Calendar UI state
- ask `calendarRegistry` for available module contributions
- call available contribution loaders
- compose results into `CalendarDataResponse`
- support `AbortController` cancellation
- receive the registry explicitly rather than importing it as a hidden singleton dependency

Preferred shape:

```ts
calendarDataService.loadData(calendarState, runtimeContext, calendarRegistry);
```

## CalendarRegistry

`CalendarRegistry` should remain a plain TypeScript class/service.

It may own:

- registered views
- registered module data contributions
- registered view detail actions/contributors
- registered toolbar actions if needed

It should not be a Pinia store.

It should not contain module-specific Calendar business logic.

Duplicate registration should not be silently ignored. Throw an error or log a clear warning.

## CalendarActionRegistry

`CalendarActionRegistry` owns Calendar create/action extension points.

Create actions should support runtime/context availability:

```ts
isAvailable?: (
  createContext: CalendarCreateContext,
  runtimeContext: CalendarRuntimeContext
) => boolean;
```

Calendar core may discover available create actions, but modules should own their own create forms and save endpoints.

Do not build a Calendar-owned Scheduling/Training create modal.

## Pinia Calendar Store

Use Pinia only for durable Calendar UI state.

Appropriate Pinia state:

- `activeViewId`
- `dateRange`
- `period`
- `locationId`
- `filters`
- `selectedEventId`
- `selectedResourceId`

Do not store in Pinia:

- `CalendarRegistry`
- `CalendarDataService`
- `CalendarActionRegistry`
- registered views
- registered module contributions
- registered actions
- API client instances
- full event objects when an event ID is enough

Selected event details should usually be derived from the current `CalendarDataResponse` using `selectedEventId`.

## Date and Time Rules

- Use explicit `period` state to determine display mode.
- Do not infer display mode from date-range length.
- Avoid `toISOString()` for local calendar date formatting when the intended value is a local date.
- For all-day events, pass date-only values to FullCalendar:
  - `start: "2026-01-01"`
  - `end: "2026-01-02"`
  - `allDay: true`

- Do not parse all-day `YYYY-MM-DD` strings with `new Date(...)` for display.
- Use a date-only formatter for all-day event labels/details.
- Use UTC instants only where the value represents an actual instant in time.

## Event Type Convention

Frontend `CalendarEventBase.type` should be a cross-module discriminator.

For Calendar-owned events, use:

```text
calendar.general
calendar.holiday
calendar.deadline
```

`eventTypeCode` may still hold the backend/local type code:

```text
general
holiday
deadline
```

When generating CSS class names from event type/code, sanitize the value first.

## CSS and FullCalendar

- Keep FullCalendar-specific overrides centralized in `calendar.fullcalendar.css`.
- Keep component-scoped CSS minimal.
- Prefer existing shared components, Vuetify components, and design tokens over large custom BEM blocks.
- Do not style nonfunctional placeholder controls.
- Do not hardcode Scheduling UI actions such as `Distribute`, `Publish`, or `Create Shift` in generic Calendar components.

## Extension Rules

### Adding a New Calendar View

1. Create a view contribution file under `views/`.
2. Define a stable `id`, label, component, and model builder.
3. Register the view from `CalendarModule.ts`.
4. Do not add backend calls inside the view.
5. Use selectors to read contribution data.

### Adding a New Module Data Contribution

1. Implement the contribution in the owning module.
2. The contribution should call the owning module's backend endpoint.
3. Return a `CalendarContributionData` object with a stable `contributionId`.
4. Register the contribution during module registration.
5. Do not modify Calendar core loaders for module-specific data.

### Adding a New Create Action

1. Define the create action in the owning module.
2. Register it with `calendarActionRegistry`.
3. Use `isAvailable` to check module enablement and permissions.
4. The owning module should open its own create form and call its own save endpoint.
5. Calendar should only provide create context, such as date/time/resource.

### Adding Event Details

For future module-specific detail panels, prefer a contributor/action model that supports multiple detail contributors per view/event.

Do not assume there is only one detail action per view.

## Do Not Do

- Do not add Scheduling or Training code to the Calendar module.
- Do not make Calendar backend endpoints view-specific.
- Do not pass `viewId` to backend Calendar data endpoints.
- Do not move registries into Pinia.
- Do not store full selected event objects in Pinia if an event ID is enough.
- Do not hardcode module-specific toolbar buttons in Calendar core.
- Do not make FullCalendar standard time-grid do highly custom Scheduling layouts. Use a module-specific custom view when needed.

## Validation

Run targeted frontend/backend validation after Calendar changes:

```bash
dotnet build api/Unified.Api/Unified.Api.csproj
```

```bash
npm run build
```

Use the project’s existing frontend package scripts if they differ.

For feature-flag wiring, smoke test both states:

```text
CalendarModule=true  -> Calendar route and /api/calendar/events are available
CalendarModule=false -> Calendar route/controller are not exposed or fail cleanly by existing conventions
```
