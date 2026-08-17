import { beforeEach, describe, expect, it, vi } from 'vitest';

describe('calendar scheduling module integration', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it('registers scheduling calendar contributions, views, and actions only once', async () => {
    const [{ registerModule }, { calendarRegistry }, { calendarActionRegistry }] = await Promise.all([
      import('@/modules/scheduling/CalendarSchedulingModule'),
      import('@/modules/calendar/registry/calendarRegistry'),
      import('@/modules/calendar/registry/calendarActionRegistry'),
    ]);

    registerModule();
    registerModule();

    expect(calendarRegistry.getAvailableViews({ featureFlags: {} }).map((view) => view.id)).toContain(
      'calendar.matrix-schedule',
    );
    expect(
      calendarRegistry
        .getAvailableModuleContributions(
          { featureFlags: { Scheduling: { enabled: true } } },
          { startDate: '2025-01-01', endDate: '2025-01-08', filters: {} },
        )
        .map((contribution) => contribution.contributionId),
    ).toContain('scheduling.shift-events');
    expect(
      calendarActionRegistry.getCreateActions(
        { startDate: '2025-01-01', endDate: '2025-01-08', activeViewId: 'calendar.matrix-schedule', filters: {} },
        { featureFlags: { Scheduling: { enabled: true } } },
      ),
    ).toHaveLength(1);
  });
});
