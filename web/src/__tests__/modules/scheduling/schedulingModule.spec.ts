import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Permissions } from '@/api-access/generated/models';

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

    const runtimeContext = {
      featureFlags: { Scheduling: { enabled: true } },
      permissions: Object.values(Permissions),
    };

    expect(calendarRegistry.getAvailableViews(runtimeContext).map((view) => view.id)).toEqual(
      expect.arrayContaining(['calendar.matrix-schedule', 'calendar.matrix-assignment']),
    );
    const contributionIds = calendarRegistry
      .getAvailableModuleContributions(runtimeContext, {
        startDate: '2025-01-01',
        endDate: '2025-01-08',
        filters: {},
      })
      .map((contribution) => contribution.contributionId);
    expect(contributionIds).toEqual(expect.arrayContaining(['scheduling.events', 'scheduling.assignment-resources']));
    expect(contributionIds).not.toContain('scheduling.assignment-events');
    expect(
      calendarRegistry
        .getAvailableModuleContributions(
          { featureFlags: { Scheduling: { enabled: true } }, permissions: [Permissions.AssignmentsView] },
          { startDate: '2025-01-01', endDate: '2025-01-08', filters: {} },
        )
        .map((contribution) => contribution.contributionId),
    ).toContain('scheduling.assignment-resources');
    expect(
      calendarActionRegistry.getCreateActions(
        { startDate: '2025-01-01', endDate: '2025-01-08', activeViewId: 'calendar.matrix-schedule', filters: {} },
        runtimeContext,
      ),
    ).toHaveLength(1);

    const actionContext = {
      actionId: 'calendar-scheduling.add-assignment',
      panel: { label: 'ASSIGNMENTS', actionId: 'calendar-scheduling.add-assignment', items: [] },
      model: {
        timeZone: 'America/Vancouver',
        days: [{ date: '2026-08-21', label: 'Fri, Aug 21', isToday: true }],
        primaryColumn: { label: 'TEAM', resources: [] },
        cells: [],
      },
    };
    const [addAssignmentAction] = calendarActionRegistry.getMatrixSidePanelActions(actionContext, runtimeContext);

    expect(addAssignmentAction).toBeDefined();
    await addAssignmentAction?.execute(actionContext, runtimeContext);
    expect(assignmentModalState.isCalendarSchedulingAssignmentModalOpen.value).toBe(true);
    expect(assignmentModalState.calendarSchedulingAssignmentModalDate.value).toBe('2026-08-21');
    expect(
      calendarActionRegistry.getDropActions(
        {
          source: 'side-panel',
          itemId: 'assignment-definition-8',
          itemType: 'assignment',
          payload: { assignmentDefinitionId: 8 },
        },
        { resourceId: 'user-1', resourceType: 'user', date: '2025-01-02' },
        actionContext.model,
        {
          featureFlags: { Scheduling: { enabled: true } },
          permissions: [Permissions.AssignmentsCreate, Permissions.AssignmentsAssign],
        },
      ),
    ).toHaveLength(1);
  });
});
