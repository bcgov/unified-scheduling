# Unified.Calendar

Shared calendar infrastructure for domain modules (Leave, Scheduling) that model recurring
entry/series data on top of the common `Event`/`EventSeries` tables. This README covers the
generic entry/series pipeline that Leave and Shift (Scheduling) now both build on.

## Why

Leave and Shift each have their own request/response models, but both need the same plumbing:
build an `Event`/`EventSeries` from a request, update one in place, and decide whether an
entry/series is still safe to delete. Rather than duplicating that logic per domain, it lives
once in `Unified.Calendar` and each domain plugs in only the parts that are genuinely different
(mapping domain-specific fields, syncing users, etc.) via a small factory interface.

## Core pieces

| Piece | Role |
|---|---|
| `ICalendarEventService<TRequest, TEntity>` | `BuildEvent` / `UpdateEvent` / `IsDeletable` — the operations a domain service calls |
| `CalendarEventEntryService<TRequest, TEntity>` | Generic implementation of `ICalendarEventService` for single (non-recurring) entries |
| `CalendarEventSeriesService<TRequest, TEntity>` | Generic implementation of `ICalendarEventService` for recurring series |
| `ICalendarEventEntryFactory<TRequest, TEntity>` | Domain hook: `CreateEntry` / `GetEvent` / `UpdateEntry` |
| `ICalendarEventSeriesFactory<TRequest, TResult>` | Domain hook: `CreateSeries` / `GetEventSeries` / `UpdateSeries` / `HasRecurrenceChanged` (default impl provided) |
| `CalendarEventServiceProvider<TEntryRequest, TEntry, TSeriesRequest, TSeries>` | Exposes whichever of `EntryService` / `SeriesService` are registered for a domain, via `SupportsEntry` / `SupportsSeries` |
| `CalendarEventGuards` | Shared invariant checks: `EnsureEventType`, `EnsureEventSeriesType`, `EnsureEntryIsDraft`, `EnsureSeriesIsDraft` |
| `CalendarEventServiceCollectionExtensions` | `AddCalendarEventEntry<...>()` / `AddCalendarEventSeries<...>()` DI registration helpers |

`CalendarEventEntryService.UpdateEvent`/`IsDeletable` already call `CalendarEventGuards` internally
(type + draft-status checks, and "recurring occurrences must be deleted through their series" for
entries linked to a series), so domains don't need to repeat those checks around every call.

## Using it in a domain module

1. **Implement the factory** for your domain's request/entity types (one class can implement both
   the entry and series factory interfaces if useful — see `ShiftEventFactory`):

   ```csharp
   public sealed class MyEventFactory(UnifiedDbContext db)
       : ICalendarEventEntryFactory<MyEntryRequest, MyEntry>,
         ICalendarEventSeriesFactory<MySeriesRequest, MySeries>
   {
       public MyEntry CreateEntry(MyEntryRequest request, Event eventEntity) => ...;
       public Event GetEvent(MyEntry entity) => entity.Event!;
       public MyEntry UpdateEntry(MyEntryRequest request, MyEntry existing) => ...;

       public MySeries CreateSeries(MySeriesRequest request, EventSeries eventSeries) => ...;
       public EventSeries GetEventSeries(MySeries entity) => entity.EventSeries!;
       public MySeries UpdateSeries(MySeriesRequest request, MySeries existing) => ...;
   }
   ```

2. **Register it** in the module's DI setup:

   ```csharp
   services.AddCalendarEventEntry<MyEntryRequest, MyEntry, MyEventFactory>();
   services.AddCalendarEventSeries<MySeriesRequest, MySeries, MyEventFactory>();
   services.TryAddScoped<CalendarEventServiceProvider<MyEntryRequest, MyEntry, MySeriesRequest, MySeries>>();
   ```

   Wrap that provider in a small domain-named class (e.g. `ShiftServiceProvider`,
   `LeaveServiceProvider`) so your domain service depends on a typed provider instead of the
   generic one directly.

3. **Call it from your domain service** instead of hand-building entities:

   ```csharp
   var entity = serviceProvider.EntryService.BuildEvent(
       request, SchedulingConstants.SourceModule, SchedulingConstants.ShiftEventTypeCode
   );
   ...
   if (!serviceProvider.EntryService.IsDeletable(entity))
       throw new InvalidOperationException("...");
   ```

   Only register entry or series support if your domain needs it — `SupportsEntry` /
   `SupportsSeries` let callers check before use, and an unregistered side throws
   `NotSupportedException` on access.

4. **Keep bespoke update logic bespoke.** `UpdateEvent` is a single black-box call; if your domain
   needs to resolve/validate a different parent series mid-update, or must capture old values
   before mutation and branch into regeneration vs. child propagation afterward (as Shift's series
   update does), call `CalendarEventGuards` directly and apply your own mapper instead of forcing
   that flow through `UpdateEvent`. Create/Delete are the parts of the pipeline worth sharing in
   that case.

## Reference implementations

- **Leave** (`Unified.Leave.Services.SchedulingLeaveService` / `LeaveServiceProvider`) uses the
  full pipeline for Create, Update, and Delete.
- **Shift** (`Unified.Scheduling.Services.ShiftService` / `ShiftServiceProvider` /
  `Unified.Scheduling.Factories.ShiftEventFactory`) uses the pipeline for Create and Delete only;
  Update stays bespoke for the reasons above.
