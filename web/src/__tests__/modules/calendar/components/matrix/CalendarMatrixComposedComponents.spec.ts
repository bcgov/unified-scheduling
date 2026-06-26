import { mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import type { CalendarEventBase, CalendarRuntimeContext } from '@/modules/calendar/calendarTypes';
import CalendarMatrixCell from '@/modules/calendar/components/matrix/CalendarMatrixCell.vue';
import CalendarMatrixGrid from '@/modules/calendar/components/matrix/CalendarMatrixGrid.vue';
import { calendarMatrixContextKey } from '@/modules/calendar/components/matrix/calendarMatrixContext';
import { CalendarMatrixActionType } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import type {
  CalendarMatrixCell as CalendarMatrixCellModel,
  CalendarMatrixDragPayload,
  CalendarMatrixResource,
  CalendarMatrixViewModel,
} from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import { useCalendarStore } from '@/modules/calendar/calendarStore';

const event: CalendarEventBase = {
  id: 'event-1',
  type: 'calendar.event',
  sourceModule: 'calendar',
  title: 'Event One',
  start: '2025-01-13T09:00:00Z',
};

const resource: CalendarMatrixResource = {
  id: 'resource-1',
  type: 'room',
  title: 'Room 101',
  action: {
    actionId: 'add-resource',
    ariaLabel: 'Add resource',
    label: 'Add',
  },
};

const cell: CalendarMatrixCellModel = {
  resourceId: 'resource-1',
  date: '2025-01-13',
  headers: [
    {
      id: 'header-1',
      text: 'Header 1',
      actionId: 'open-header',
      action: {
        actionId: 'header-action',
        text: 'Inspect',
        type: CalendarMatrixActionType.Button,
      },
    },
  ],
  groups: [
    {
      id: 'group-1',
      color: '#224466',
      showColorBar: true,
      action: {
        actionId: 'group-action',
        text: 'Open',
        type: CalendarMatrixActionType.Button,
      },
      events: [{ event }],
    },
  ],
};

const model: CalendarMatrixViewModel = {
  days: [{ date: '2025-01-13', label: 'Mon', isToday: true }],
  primaryColumn: {
    label: 'Resources',
    resources: [resource],
  },
  cells: [cell],
  sidePanel: {
    label: 'People',
    actionId: 'add-person',
    actionLabel: 'Add person',
    items: [{ id: 'item-1', type: 'user', title: 'Ada', draggable: true }],
  },
};

const runtimeContext: CalendarRuntimeContext = { featureFlags: {} };

function createDataTransfer(jsonPayload?: unknown) {
  const store = new Map<string, string>();
  if (jsonPayload !== undefined) {
    store.set('application/json', typeof jsonPayload === 'string' ? jsonPayload : JSON.stringify(jsonPayload));
  }

  return {
    types: Array.from(store.keys()),
    setData: vi.fn((type: string, value: string) => {
      store.set(type, value);
    }),
    getData: vi.fn((type: string) => store.get(type) ?? ''),
    setDragImage: vi.fn(),
    dropEffect: 'none',
  };
}

async function mountWithApp(component: unknown, options: Record<string, unknown> = {}) {
  const { mountPlugins, pinia } = await createTestApp({ loadConfig: false });

  return {
    pinia,
    wrapper: mount(
      component as never,
      {
        ...options,
        global: {
          ...(options.global as Record<string, unknown> | undefined),
          plugins: mountPlugins,
        },
      } as never,
    ),
  };
}

afterEach(() => {
  vi.restoreAllMocks();
  vi.doUnmock('@/modules/calendar/registry/calendarActionRegistry');
});

describe('CalendarMatrixCell', () => {
  it('renders headers and events, merges group display, and emits interactions', async () => {
    const dropOnCell = vi.fn();
    const activeDragPayload = { source: 'side-panel', itemId: 'drag-1', itemType: 'user' } as CalendarMatrixDragPayload;

    const { wrapper } = await mountWithApp(CalendarMatrixCell, {
      props: { cell, resource, isToday: true },
      global: {
        provide: {
          [calendarMatrixContextKey as symbol]: {
            selectedEventId: { value: undefined },
            selectedResourceId: { value: undefined },
            activeDragPayload: { value: activeDragPayload },
            selectEvent: vi.fn(),
            selectResource: vi.fn(),
            startDrag: vi.fn(),
            clearDrag: vi.fn(),
            dropOnCell,
          },
        },
        stubs: {
          'v-icon': true,
        },
      },
    });

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['is-today', 'is-drop-active']));
    expect(wrapper.attributes('role')).toBe('gridcell');
    expect(wrapper.attributes('aria-label')).toContain('Room 101 on 2025-01-13');
    expect(wrapper.findComponent({ name: 'CalendarMatrixCellHeader' }).exists()).toBe(true);
    expect(wrapper.findComponent({ name: 'CalendarMatrixEventBlock' }).exists()).toBe(true);

    await wrapper.findComponent({ name: 'CalendarMatrixCellHeader' }).vm.$emit('click', cell.headers?.[0]);
    await wrapper.findComponent({ name: 'CalendarMatrixCellHeader' }).vm.$emit('action', {
      cell,
      header: cell.headers?.[0],
      actionId: 'header-action',
      actionType: CalendarMatrixActionType.Button,
    });
    await wrapper.findComponent({ name: 'CalendarMatrixEventBlock' }).vm.$emit('eventClick', event);
    await wrapper.findComponent({ name: 'CalendarMatrixEventBlock' }).vm.$emit('eventAction', {
      event,
      actionId: 'group-action',
      actionType: CalendarMatrixActionType.Button,
    });
    await wrapper.findComponent({ name: 'CalendarMatrixEventBlock' }).vm.$emit('dragStart', activeDragPayload);

    const dropTransfer = createDataTransfer(activeDragPayload);
    await wrapper.trigger('dragenter', { dataTransfer: dropTransfer });
    await wrapper.trigger('dragover', { dataTransfer: dropTransfer });
    await wrapper.trigger('drop', { dataTransfer: dropTransfer });

    expect(wrapper.emitted('headerClick')?.[0]?.[0]).toMatchObject({ header: cell.headers?.[0] });
    expect(wrapper.emitted('headerAction')?.[0]?.[0]).toMatchObject({ actionId: 'header-action' });
    expect(wrapper.emitted('eventClick')?.[0]).toEqual([event]);
    expect(wrapper.emitted('eventAction')?.[0]?.[0]).toMatchObject({ actionId: 'group-action' });
    expect(wrapper.emitted('dragStart')?.[0]?.[0]).toMatchObject({ itemId: 'drag-1' });
    expect(dropOnCell).toHaveBeenCalledWith({ resourceId: 'resource-1', resourceType: 'room', date: '2025-01-13' });
    expect(wrapper.emitted('cellDrop')?.[0]?.[0]).toMatchObject({
      drag: activeDragPayload,
      drop: { resourceId: 'resource-1', resourceType: 'room', date: '2025-01-13' },
    });
  });

  it('renders an empty cell and ignores drops without a readable payload', async () => {
    const { wrapper } = await mountWithApp(CalendarMatrixCell, {
      props: {
        cell: { resourceId: 'resource-2', date: '2025-01-14', groups: [] },
      },
      global: {
        provide: {
          [calendarMatrixContextKey as symbol]: {
            selectedEventId: { value: undefined },
            selectedResourceId: { value: undefined },
            activeDragPayload: { value: undefined },
            selectEvent: vi.fn(),
            selectResource: vi.fn(),
            startDrag: vi.fn(),
            clearDrag: vi.fn(),
            dropOnCell: vi.fn(),
          },
        },
      },
    });

    expect(wrapper.classes()).toContain('is-empty');
    await wrapper.trigger('drop', { dataTransfer: createDataTransfer('{invalid') });
    expect(wrapper.emitted('cellDrop')).toBeUndefined();
  });
});

describe('CalendarMatrixGrid', () => {
  it('renders header, resource rows, cells, and forwards child events', async () => {
    const { wrapper } = await mountWithApp(CalendarMatrixGrid, {
      props: { model },
      global: {
        stubs: {
          CalendarMatrixHeader: {
            props: ['primaryColumnLabel', 'days'],
            template: '<div class="header-stub">{{ primaryColumnLabel }}-{{ days.length }}</div>',
          },
          CalendarMatrixResourceRow: {
            props: ['resource'],
            template:
              '<button class="resource-row-stub" @click="$emit(\'addResource\', resource)">{{ resource.title }}</button>',
          },
          CalendarMatrixCell: {
            props: ['cell', 'resource', 'isToday'],
            template:
              "<button class=\"cell-stub\" @click=\"$emit('cellDrop', { drag: { source: 'side-panel', itemId: 'item-1', itemType: 'user' }, drop: { resourceId: cell.resourceId, date: cell.date } })\">{{ cell.resourceId }}|{{ resource?.title }}|{{ isToday }}</button>",
          },
        },
      },
    });

    expect(wrapper.find('.header-stub').text()).toContain('Resources-1');
    expect(wrapper.get('.calendar-matrix-grid__table').attributes('role')).toBe('grid');
    expect(wrapper.get('.calendar-matrix-grid__table').attributes('aria-label')).toBe('Resources calendar matrix');
    expect(wrapper.get('.calendar-matrix-grid__table').attributes('style')).toContain('grid-template-columns');
    expect(wrapper.get('.calendar-matrix-grid__row').attributes('role')).toBe('row');

    await wrapper.get('.resource-row-stub').trigger('click');
    await wrapper.get('.cell-stub').trigger('click');

    expect(wrapper.emitted('resourceAdd')?.[0]).toEqual([resource]);
    expect(wrapper.emitted('cellDrop')?.[0]?.[0]).toMatchObject({
      drop: { resourceId: 'resource-1', date: '2025-01-13' },
    });
  });

  it('shows the empty state when matrix data is missing', async () => {
    const { wrapper } = await mountWithApp(CalendarMatrixGrid, {
      props: {
        model: {
          days: [],
          primaryColumn: { label: 'Resources', resources: [] },
          cells: [],
        },
      },
      global: {
        stubs: {
          CalendarMatrixHeader: true,
          CalendarMatrixResourceRow: true,
          CalendarMatrixCell: true,
        },
      },
    });

    expect(wrapper.text()).toContain('No matrix data to display.');
  });
});

describe('CalendarMatrixView', () => {
  it('provides matrix context, updates store selection, and executes single registry actions', async () => {
    vi.resetModules();

    const executeDrop = vi.fn();
    const executeSidePanel = vi.fn();
    const executeResource = vi.fn();
    const executeHeader = vi.fn();
    const executeEvent = vi.fn();

    vi.doMock('@/modules/calendar/registry/calendarActionRegistry', () => ({
      calendarActionRegistry: {
        getDropActions: vi.fn(() => [{ id: 'drop', execute: executeDrop }]),
        getMatrixSidePanelActions: vi.fn(() => [{ id: 'side', execute: executeSidePanel }]),
        getMatrixResourceActions: vi.fn(() => [{ id: 'resource', execute: executeResource }]),
        getMatrixCellHeaderActions: vi.fn((context: { actionType?: string }) =>
          context.actionType
            ? [{ id: 'header-action', execute: executeHeader }]
            : [{ id: 'header-click', execute: executeHeader }],
        ),
        getMatrixEventBlockActions: vi.fn(() => [{ id: 'event', execute: executeEvent }]),
      },
    }));

    const { default: MatrixView } = await import('@/modules/calendar/components/matrix/CalendarMatrixView.vue');
    const { wrapper, pinia } = await mountWithApp(MatrixView, {
      props: { model, runtimeContext },
      global: {
        stubs: {
          CalendarMatrixGrid: {
            props: ['model'],
            template:
              "<div><button class=\"grid-event-click\" @click=\"$emit('eventClick', { id: 'event-1', type: 'calendar.event', sourceModule: 'calendar', title: 'Event One', start: '2025-01-13T09:00:00Z' })\" /><button class=\"grid-cell-drop\" @click=\"$emit('cellDrop', { drag: { source: 'side-panel', itemId: 'item-1', itemType: 'user' }, drop: { resourceId: 'resource-1', date: '2025-01-13' } })\" /><button class=\"grid-header-click\" @click=\"$emit('headerClick', { cell: { resourceId: 'resource-1', date: '2025-01-13', groups: [] }, header: { text: 'Header', actionId: 'open-header' } })\" /><button class=\"grid-header-action\" @click=\"$emit('headerAction', { cell: { resourceId: 'resource-1', date: '2025-01-13', groups: [] }, header: { text: 'Header' }, actionId: 'header-action', actionType: 'button' })\" /><button class=\"grid-event-action\" @click=\"$emit('eventAction', { event: { id: 'event-1', type: 'calendar.event', sourceModule: 'calendar', title: 'Event One', start: '2025-01-13T09:00:00Z' }, actionId: 'event-action', actionType: 'button' })\" /><button class=\"grid-resource-add\" @click=\"$emit('resourceAdd', { id: 'resource-1', type: 'room', title: 'Room 101', action: { actionId: 'add-resource', ariaLabel: 'Add resource' } })\" /><button class=\"grid-drag-start\" @click=\"$emit('dragStart', { source: 'side-panel', itemId: 'item-1', itemType: 'user' })\" /></div>",
          },
          CalendarMatrixSidePanel: {
            props: ['panel'],
            template:
              "<div><button class=\"side-panel-action\" @click=\"$emit('action')\" /><button class=\"side-panel-drag\" @click=\"$emit('itemDragStart', { source: 'side-panel', itemId: 'item-2', itemType: 'user' })\" /></div>",
          },
        },
      },
    });
    const store = useCalendarStore(pinia);
    store.setSelectedResource('resource-1');

    await wrapper.get('.grid-event-click').trigger('click');
    await wrapper.get('.grid-cell-drop').trigger('click');
    await wrapper.get('.grid-header-click').trigger('click');
    await wrapper.get('.grid-header-action').trigger('click');
    await wrapper.get('.grid-event-action').trigger('click');
    await wrapper.get('.grid-resource-add').trigger('click');
    await wrapper.get('.side-panel-action').trigger('click');
    await wrapper.get('.grid-drag-start').trigger('click');
    await wrapper.get('.side-panel-drag').trigger('click');

    expect(store.selectedEventId).toBe('event-1');
    expect(executeDrop).toHaveBeenCalledOnce();
    expect(executeSidePanel).toHaveBeenCalledOnce();
    expect(executeResource).toHaveBeenCalledOnce();
    expect(executeHeader).toHaveBeenCalledTimes(2);
    expect(executeEvent).toHaveBeenCalledOnce();
    expect(wrapper.emitted('eventClick')?.[0]?.[0]).toMatchObject({ id: 'event-1' });
    expect(wrapper.emitted('dragStart')).toHaveLength(2);
  });

  it('ignores unavailable actions, throws for duplicate actions, and renders unsupported states', async () => {
    vi.resetModules();

    vi.doMock('@/modules/calendar/registry/calendarActionRegistry', () => ({
      calendarActionRegistry: {
        getDropActions: vi.fn(() => []),
        getMatrixSidePanelActions: vi.fn(() => []),
        getMatrixResourceActions: vi.fn(() => []),
        getMatrixCellHeaderActions: vi.fn((context: { actionId?: string }) =>
          context.actionId === 'header-action'
            ? [
                { id: 'one', execute: vi.fn() },
                { id: 'two', execute: vi.fn() },
              ]
            : [],
        ),
        getMatrixEventBlockActions: vi.fn(() => [
          { id: 'one', execute: vi.fn() },
          { id: 'two', execute: vi.fn() },
        ]),
      },
    }));

    const { default: MatrixView } = await import('@/modules/calendar/components/matrix/CalendarMatrixView.vue');
    const errorHandler = vi.fn();

    const { wrapper } = await mountWithApp(MatrixView, {
      props: { model, runtimeContext },
      global: {
        config: {
          errorHandler,
        },
        stubs: {
          CalendarMatrixGrid: {
            props: ['model'],
            template:
              "<div><button class=\"grid-cell-drop\" @click=\"$emit('cellDrop', { drag: { source: 'side-panel', itemId: 'item-1', itemType: 'user' }, drop: { resourceId: 'resource-1', date: '2025-01-13' } })\" /><button class=\"grid-header-click\" @click=\"$emit('headerClick', { cell: { resourceId: 'resource-1', date: '2025-01-13', groups: [] }, header: { text: 'Header', actionId: 'open-header' } })\" /><button class=\"grid-header-action\" @click=\"$emit('headerAction', { cell: { resourceId: 'resource-1', date: '2025-01-13', groups: [] }, header: { text: 'Header' }, actionId: 'header-action', actionType: 'button' })\" /><button class=\"grid-event-action\" @click=\"$emit('eventAction', { event: { id: 'event-1', type: 'calendar.event', sourceModule: 'calendar', title: 'Event One', start: '2025-01-13T09:00:00Z' }, actionId: 'event-action', actionType: 'button' })\" /><button class=\"grid-resource-add\" @click=\"$emit('resourceAdd', { id: 'resource-1', type: 'room', title: 'Room 101', action: { actionId: 'add-resource', ariaLabel: 'Add resource' } })\" /></div>",
          },
          CalendarMatrixSidePanel: {
            props: ['panel'],
            template: '<button class="side-panel-action" @click="$emit(\'action\')" />',
          },
        },
      },
    });

    await wrapper.get('.grid-cell-drop').trigger('click');
    await wrapper.get('.grid-header-click').trigger('click');
    await wrapper.get('.grid-resource-add').trigger('click');
    await wrapper.get('.side-panel-action').trigger('click');

    expect(wrapper.emitted('cellDrop')).toBeUndefined();
    expect(wrapper.emitted('headerClick')).toBeUndefined();
    expect(wrapper.emitted('resourceAdd')).toBeUndefined();
    expect(wrapper.emitted('sidePanelAction')).toBeUndefined();
    await wrapper.get('.grid-header-action').trigger('click');
    await wrapper.get('.grid-event-action').trigger('click');
    expect(errorHandler).toHaveBeenCalledWith(
      new Error("Multiple calendar matrix cell header actions handle 'header-action'."),
      expect.any(Object),
      expect.any(String),
    );
    expect(errorHandler).toHaveBeenCalledWith(
      new Error("Multiple calendar matrix event block actions handle 'event-action'."),
      expect.any(Object),
      expect.any(String),
    );

    const { wrapper: unsupportedWrapper } = await mountWithApp(MatrixView, {
      props: {
        model: {
          ...model,
          unsupportedMessage: 'Unsupported period',
        },
      },
      global: {
        stubs: {
          CalendarMatrixGrid: true,
          CalendarMatrixSidePanel: true,
        },
      },
    });

    expect(unsupportedWrapper.text()).toContain('Unsupported period');
    expect(unsupportedWrapper.findComponent({ name: 'CalendarMatrixGrid' }).exists()).toBe(false);
  });
});
