import { beforeEach, describe, expect, it, vi } from 'vitest';

describe('calendar scheduling module integration', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it('registers scheduling calendar contributions, views, and actions only once', async () => {
    const [{ registerModule }, { calendarRegistry }, { calendarActionRegistry }, assignmentModalState] =
      await Promise.all([
        import('@/modules/scheduling/CalendarSchedulingModule'),
        import('@/modules/calendar/registry/calendarRegistry'),
        import('@/modules/calendar/registry/calendarActionRegistry'),
        import('@/modules/scheduling/calendarSchedulingState'),
      ]);

    registerModule();
    registerModule();

    expect(calendarRegistry.getAvailableViews({ featureFlags: {} }).map((view) => view.id)).toEqual(
      expect.arrayContaining(['calendar.matrix-schedule', 'calendar.matrix-assignment']),
    );
    expect(
      calendarRegistry
        .getAvailableModuleContributions(
          { featureFlags: { Scheduling: { enabled: true } } },
          { startDate: '2025-01-01', endDate: '2025-01-08', filters: {} },
        )
        .map((contribution) => contribution.contributionId),
    ).toEqual(expect.arrayContaining(['scheduling.events', 'scheduling.assignment-events']));
    expect(
      calendarActionRegistry.getCreateActions(
        { startDate: '2025-01-01', endDate: '2025-01-08', activeViewId: 'calendar.matrix-schedule', filters: {} },
        { featureFlags: { Scheduling: { enabled: true } } },
      ),
    ).toHaveLength(1);
    const actionContext = {
      actionId: 'calendar-scheduling.add-assignment',
      panel: { label: 'ASSIGNMENTS', actionId: 'calendar-scheduling.add-assignment', items: [] },
      model: {
        days: [{ date: '2026-08-21', label: 'Fri, Aug 21', isToday: true }],
        primaryColumn: { label: 'TEAM', resources: [] },
        cells: [],
      },
    };
    const [addAssignmentAction] = calendarActionRegistry.getMatrixSidePanelActions(actionContext, {
      featureFlags: { Scheduling: { enabled: true } },
    });

    expect(addAssignmentAction).toBeDefined();
    await addAssignmentAction?.execute(actionContext, { featureFlags: { Scheduling: { enabled: true } } });
    expect(assignmentModalState.isCalendarSchedulingAssignmentModalOpen.value).toBe(true);
    expect(assignmentModalState.calendarSchedulingAssignmentModalDate.value).toBe('2026-08-21');
  });
});
