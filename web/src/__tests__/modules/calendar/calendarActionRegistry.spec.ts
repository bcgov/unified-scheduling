import { describe, expect, it, vi } from 'vitest';
import { CalendarEventType } from '@/api-access/generated/models';
import { CalendarActionRegistry } from '@/modules/calendar/registry/calendarActionRegistry';
import { CalendarMatrixActionType } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import type { CalendarRuntimeContext } from '@/modules/calendar/calendarTypes';
import type {
  CalendarMatrixCellHeaderAction,
  CalendarMatrixEventBlockAction,
  CalendarMatrixResourceAction,
  CalendarMatrixSidePanelAction,
} from '@/modules/calendar/registry/calendarActionRegistryTypes';

const runtimeContext: CalendarRuntimeContext = { featureFlags: {} };
const model = {
  days: [],
  primaryColumn: { label: 'Resources', resources: [] },
  cells: [],
};
const cell = { resourceId: 'resource-1', date: '2025-01-13', groups: [] };
const header = { text: 'Header' };
const event = {
  id: 'event-1',
  type: CalendarEventType.CalendarEvent,
  sourceModule: 'calendar',
  title: 'Event',
  start: '2025-01-13T09:00:00Z',
};
const resource = { id: 'resource-1', type: 'room', title: 'Room 101' };
const panel = { label: 'People', items: [] };

describe('CalendarActionRegistry matrix actions', () => {
  it('routes side panel actions by explicit action id before availability predicates', () => {
    const registry = new CalendarActionRegistry();
    const executeA = vi.fn();
    const executeB = vi.fn();

    registry.registerMatrixSidePanelAction(createSidePanelAction('A', executeA));
    registry.registerMatrixSidePanelAction(createSidePanelAction('B', executeB));

    expect(registry.getMatrixSidePanelActions({ panel, model, actionId: 'B' }, runtimeContext)).toHaveLength(1);
    expect(registry.getMatrixSidePanelActions({ panel, model, actionId: 'missing' }, runtimeContext)).toHaveLength(0);
    expect(registry.getMatrixSidePanelActions({ panel, model }, runtimeContext)).toHaveLength(0);
  });

  it('routes resource actions by explicit action id', () => {
    const registry = new CalendarActionRegistry();

    registry.registerMatrixResourceAction(createResourceAction('A'));
    registry.registerMatrixResourceAction(createResourceAction('B'));

    expect(registry.getMatrixResourceActions({ resource, model, actionId: 'A' }, runtimeContext)).toHaveLength(1);
    expect(registry.getMatrixResourceActions({ resource, model, actionId: 'B' }, runtimeContext)).toHaveLength(1);
    expect(registry.getMatrixResourceActions({ resource, model, actionId: 'C' }, runtimeContext)).toHaveLength(0);
  });

  it('routes header actions by explicit action id and keeps isAvailable as an additional predicate', () => {
    const registry = new CalendarActionRegistry();

    registry.registerMatrixCellHeaderAction(createHeaderAction('A'));
    registry.registerMatrixCellHeaderAction({ ...createHeaderAction('B'), isAvailable: () => false });

    expect(
      registry.getMatrixCellHeaderActions(
        { cell, header, model, actionId: 'A', actionType: CalendarMatrixActionType.Button },
        runtimeContext,
      ),
    ).toHaveLength(1);
    expect(
      registry.getMatrixCellHeaderActions(
        { cell, header, model, actionId: 'B', actionType: CalendarMatrixActionType.Button },
        runtimeContext,
      ),
    ).toHaveLength(0);
  });

  it('routes event block actions by explicit action id', () => {
    const registry = new CalendarActionRegistry();

    registry.registerMatrixEventBlockAction(createEventAction('A'));
    registry.registerMatrixEventBlockAction(createEventAction('B'));

    expect(
      registry.getMatrixEventBlockActions(
        { event, model, actionId: 'B', actionType: CalendarMatrixActionType.Button },
        runtimeContext,
      ),
    ).toHaveLength(1);
    expect(
      registry.getMatrixEventBlockActions(
        { event, model, actionId: 'A', actionType: CalendarMatrixActionType.Button },
        runtimeContext,
      ),
    ).toHaveLength(1);
    expect(
      registry.getMatrixEventBlockActions(
        { event, model, actionId: 'C', actionType: CalendarMatrixActionType.Button },
        runtimeContext,
      ),
    ).toHaveLength(0);
  });

  it('supports declared handled action ids and exposes duplicate matches for developer errors', () => {
    const registry = new CalendarActionRegistry();

    registry.registerMatrixEventBlockAction({ ...createEventAction('first'), handlesActionId: 'shared' });
    registry.registerMatrixEventBlockAction({ ...createEventAction('second'), handlesActionId: 'shared' });

    expect(
      registry.getMatrixEventBlockActions(
        { event, model, actionId: 'shared', actionType: CalendarMatrixActionType.Button },
        runtimeContext,
      ),
    ).toHaveLength(2);
  });
});

function createSidePanelAction(id: string, execute = vi.fn()): CalendarMatrixSidePanelAction {
  return {
    id,
    moduleId: 'test',
    execute,
  };
}

function createResourceAction(id: string): CalendarMatrixResourceAction {
  return {
    id,
    moduleId: 'test',
    execute: vi.fn(),
  };
}

function createHeaderAction(id: string): CalendarMatrixCellHeaderAction {
  return {
    id,
    moduleId: 'test',
    execute: vi.fn(),
  };
}

function createEventAction(id: string): CalendarMatrixEventBlockAction {
  return {
    id,
    moduleId: 'test',
    execute: vi.fn(),
  };
}
