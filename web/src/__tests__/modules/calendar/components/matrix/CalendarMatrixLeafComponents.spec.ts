import { mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ref } from 'vue';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import CalendarMatrixCellHeader from '@/modules/calendar/components/matrix/CalendarMatrixCellHeader.vue';
import CalendarMatrixEventBlock from '@/modules/calendar/components/matrix/CalendarMatrixEventBlock.vue';
import CalendarMatrixHeader from '@/modules/calendar/components/matrix/CalendarMatrixHeader.vue';
import CalendarMatrixResourceRow from '@/modules/calendar/components/matrix/CalendarMatrixResourceRow.vue';
import CalendarMatrixSidePanel from '@/modules/calendar/components/matrix/CalendarMatrixSidePanel.vue';
import CalendarMatrixSidePanelItem from '@/modules/calendar/components/matrix/CalendarMatrixSidePanelItem.vue';
import { calendarMatrixContextKey } from '@/modules/calendar/components/matrix/calendarMatrixContext';
import { CalendarMatrixActionType } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import type {
  CalendarMatrixCell,
  CalendarMatrixSidePanel as CalendarMatrixSidePanelModel,
  CalendarMatrixSidePanelItem as CalendarMatrixSidePanelItemModel,
} from '@/modules/calendar/components/matrix/calendarMatrixTypes';

const event: CalendarEventBase = {
  id: 'event-1',
  type: 'calendar.event',
  sourceModule: 'calendar',
  title: 'Morning Event',
  start: '2025-01-13T09:00:00Z',
  end: '2025-01-13T10:00:00Z',
};

const cell: CalendarMatrixCell = {
  resourceId: 'resource-1',
  date: '2025-01-13',
  groups: [],
};

const sidePanelItem: CalendarMatrixSidePanelItemModel = {
  id: 'item-1',
  type: 'user',
  title: 'Ada Lovelace',
  subtitle: 'Developer',
  avatarText: 'AL',
  draggable: true,
  meta: [{ label: 'Role', value: 'Primary' }],
  payload: { level: 1 },
};

const sidePanel: CalendarMatrixSidePanelModel = {
  label: 'People',
  actionId: 'add-user',
  actionLabel: 'Add',
  items: [sidePanelItem],
};

const matrixContext = {
  selectedEventId: ref<string | undefined>('event-1'),
  selectedResourceId: ref<string | undefined>('resource-1'),
  activeDragPayload: ref(),
  selectEvent: vi.fn(),
  selectResource: vi.fn(),
  startDrag: vi.fn(),
  clearDrag: vi.fn(),
  dropOnCell: vi.fn(),
};

const iconStub = {
  props: ['icon'],
  template: '<i class="icon-stub">{{ icon }}</i>',
};

async function mountWithApp(component: unknown, options: Record<string, unknown> = {}) {
  const { mountPlugins } = await createTestApp({ loadConfig: false });

  return mount(component as never, {
    ...options,
    global: {
      ...(options.global as Record<string, unknown> | undefined),
      plugins: mountPlugins,
    },
  } as never);
}

afterEach(() => {
  vi.restoreAllMocks();
  matrixContext.selectEvent.mockReset();
  matrixContext.selectResource.mockReset();
  matrixContext.startDrag.mockReset();
  matrixContext.clearDrag.mockReset();
  matrixContext.dropOnCell.mockReset();
});

describe('CalendarMatrixHeader', () => {
  it('renders the primary label and day labels', async () => {
    const wrapper = await mountWithApp(CalendarMatrixHeader, {
      props: {
        primaryColumnLabel: 'Resources',
        days: [
          { date: '2025-01-13', label: 'Mon' },
          { date: '2025-01-14', label: 'Tue', isToday: true },
        ],
      },
    });

    expect(wrapper.text()).toContain('Resources');
    expect(wrapper.text()).toContain('Mon');
    expect(wrapper.attributes('role')).toBe('row');
    expect(wrapper.findAll('[role="columnheader"]')).toHaveLength(3);
    expect(wrapper.findAll('.calendar-matrix-header__day')[1]?.classes()).toContain('is-today');
  });
});

describe('CalendarMatrixCellHeader', () => {
  it('emits click and action events for clickable headers', async () => {
    const header = {
      id: 'header-1',
      text: 'Header',
      title: 'Header title',
      actionId: 'open',
      status: 'Draft Item',
      color: '#336699',
      action: {
        actionId: 'details',
        text: 'View',
        type: CalendarMatrixActionType.Button,
      },
    };

    const wrapper = await mountWithApp(CalendarMatrixCellHeader, {
      props: { cell, header, selected: true },
      global: {
        stubs: { 'v-icon': iconStub },
      },
    });

    await wrapper.get('button.calendar-matrix-cell-header__main').trigger('click');
    await wrapper.get('button.calendar-matrix-cell-header__action').trigger('click');

    expect(wrapper.emitted('click')?.[0]).toEqual([header]);
    expect(wrapper.emitted('action')?.[0]?.[0]).toMatchObject({
      cell,
      header,
      actionId: 'details',
      actionType: CalendarMatrixActionType.Button,
    });
    expect(wrapper.classes()).toEqual(
      expect.arrayContaining(['is-selected', 'has-color', 'is-draft', 'has-action-display']),
    );
    expect(wrapper.attributes('style')).toContain('--calendar-matrix-cell-header-border-color: #336699');
  });

  it('renders static action content for custom actions and ignores main clicks without an actionId', async () => {
    const wrapper = await mountWithApp(CalendarMatrixCellHeader, {
      props: {
        cell,
        header: {
          text: 'Static Header',
          status: 'Cancelled',
          action: {
            actionId: 'custom',
            text: 'Static',
            type: CalendarMatrixActionType.Custom,
          },
        },
      },
      global: {
        stubs: { 'v-icon': iconStub },
      },
    });

    expect(wrapper.find('button.calendar-matrix-cell-header__main').exists()).toBe(false);
    expect(wrapper.find('.calendar-matrix-cell-header__action.is-static').exists()).toBe(true);
    expect(wrapper.classes()).toContain('is-cancelled');
    expect(wrapper.emitted('click')).toBeUndefined();
  });
});

describe('CalendarMatrixEventBlock', () => {
  it('emits click, action, and drag events and uses injected selection state', async () => {
    const dataTransfer = {
      setData: vi.fn(),
      setDragImage: vi.fn(),
    };

    const wrapper = await mountWithApp(CalendarMatrixEventBlock, {
      props: {
        event,
        variant: 'warning',
        showColorBar: true,
        display: {
          color: '#123456',
          status: 'Draft',
          draggable: true,
          action: {
            actionId: 'inspect',
            text: 'Inspect',
            type: CalendarMatrixActionType.Button,
          },
        },
      },
      global: {
        provide: { [calendarMatrixContextKey as symbol]: matrixContext },
        stubs: { 'v-icon': iconStub },
      },
    });

    await wrapper.get('button.calendar-matrix-event-block__main').trigger('click');
    await wrapper.get('button.calendar-matrix-event-block__action').trigger('click');
    await wrapper.get('button.calendar-matrix-event-block__main').trigger('dragstart', {
      dataTransfer,
      currentTarget: wrapper.get('button.calendar-matrix-event-block__main').element,
    });
    await wrapper.get('button.calendar-matrix-event-block__main').trigger('dragend');

    expect(matrixContext.selectEvent).toHaveBeenCalledWith('event-1');
    expect(matrixContext.startDrag).toHaveBeenCalledWith({
      source: 'event-block',
      itemId: 'event-1',
      itemType: 'calendar.event',
      payload: event,
    });
    expect(matrixContext.clearDrag).toHaveBeenCalledOnce();
    expect(wrapper.emitted('eventClick')?.[0]).toEqual([event]);
    expect(wrapper.emitted('eventAction')?.[0]?.[0]).toMatchObject({ actionId: 'inspect' });
    expect(wrapper.emitted('dragStart')?.[0]?.[0]).toMatchObject({ itemId: 'event-1', source: 'event-block' });
    expect(dataTransfer.setData).toHaveBeenCalledWith('text/plain', 'event-1');
    expect(wrapper.attributes('role')).toBeUndefined();
    expect(wrapper.get('button.calendar-matrix-event-block__main').attributes('aria-label')).toContain(
      'Morning Event',
    );
    expect(wrapper.classes()).toEqual(
      expect.arrayContaining([
        'is-selected',
        'is-draggable',
        'has-color-bar',
        'has-action-display',
        'is-draft',
        'has-warning-variant',
      ]),
    );
    expect(wrapper.attributes('style')).toContain('--calendar-event-border-color: #123456');
  });

  it('renders a static action for custom displays and falls back to active primary styling', async () => {
    const wrapper = await mountWithApp(CalendarMatrixEventBlock, {
      props: {
        event: { ...event, id: 'event-2', allDay: true, end: undefined },
        variant: 'unknown',
        display: {
          status: 'Unknown',
          action: {
            actionId: 'custom',
            text: 'Custom',
            type: CalendarMatrixActionType.Custom,
          },
        },
      },
      global: {
        stubs: { 'v-icon': iconStub },
      },
    });

    expect(wrapper.find('.calendar-matrix-event-block__action.is-static').exists()).toBe(true);
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['is-active', 'has-primary-variant']));
  });
});

describe('CalendarMatrixResourceRow', () => {
  it('renders resource metadata and emits addResource when the action button is clicked', async () => {
    const resource = {
      id: 'resource-1',
      type: 'room',
      title: 'Room 101',
      subtitle: 'Main floor',
      avatarText: 'R1',
      meta: [{ label: 'Capacity', value: '4' }],
      action: {
        actionId: 'add-resource',
        ariaLabel: 'Add resource',
        label: 'Add',
      },
    };

    const wrapper = await mountWithApp(CalendarMatrixResourceRow, {
      props: { resource },
      global: {
        stubs: { 'v-icon': iconStub },
      },
    });

    await wrapper.get('button.calendar-matrix-resource-row__add').trigger('click');

    expect(wrapper.attributes('role')).toBe('rowheader');
    expect(wrapper.attributes('aria-label')).toBe('Room 101');
    expect(wrapper.text()).toContain('Room 101');
    expect(wrapper.text()).toContain('Capacity:');
    expect(wrapper.emitted('addResource')?.[0]).toEqual([resource]);
  });
});

describe('CalendarMatrixSidePanelItem', () => {
  it('renders the item content and emits dragStart for draggable items', async () => {
    const dataTransfer = {
      setData: vi.fn(),
      setDragImage: vi.fn(),
    };

    const wrapper = await mountWithApp(CalendarMatrixSidePanelItem, {
      props: { item: sidePanelItem },
      global: {
        provide: { [calendarMatrixContextKey as symbol]: matrixContext },
      },
    });

    await wrapper.get('.calendar-matrix-side-panel-item').trigger('dragstart', {
      dataTransfer,
      currentTarget: wrapper.get('.calendar-matrix-side-panel-item').element,
    });
    await wrapper.get('.calendar-matrix-side-panel-item').trigger('dragend');

    expect(wrapper.text()).toContain('Ada Lovelace');
    expect(wrapper.attributes('role')).toBe('listitem');
    expect(wrapper.attributes('aria-label')).toBe('Ada Lovelace');
    expect(wrapper.text()).toContain('Role:');
    expect(matrixContext.startDrag).toHaveBeenCalledWith({
      source: 'side-panel',
      itemId: 'item-1',
      itemType: 'user',
      payload: { level: 1 },
    });
    expect(matrixContext.clearDrag).toHaveBeenCalledOnce();
    expect(wrapper.emitted('dragStart')?.[0]?.[0]).toMatchObject({ source: 'side-panel', itemId: 'item-1' });
  });
});

describe('CalendarMatrixSidePanel', () => {
  it('renders items, emits action, and forwards item drag events', async () => {
    const wrapper = await mountWithApp(CalendarMatrixSidePanel, {
      props: { panel: sidePanel },
      global: {
        stubs: {
          UaBtn: {
            emits: ['click'],
            template: '<button class="ua-btn-stub" @click="$emit(\'click\')"><slot /></button>',
          },
          CalendarMatrixSidePanelItem: {
            props: ['item'],
            emits: ['dragStart'],
            template:
              '<button class="side-panel-item-stub" @click="$emit(\'dragStart\', { source: \'side-panel\', itemId: item.id, itemType: item.type })">{{ item.title }}</button>',
          },
        },
      },
    });

    await wrapper.get('.ua-btn-stub').trigger('click');
    await wrapper.get('.side-panel-item-stub').trigger('click');

    expect(wrapper.attributes('aria-label')).toBe('People');
    expect(wrapper.text()).toContain('People');
    expect(wrapper.emitted('action')).toHaveLength(1);
    expect(wrapper.emitted('itemDragStart')?.[0]?.[0]).toMatchObject({ itemId: 'item-1', itemType: 'user' });
  });

  it('renders the empty state when there are no items', async () => {
    const wrapper = await mountWithApp(CalendarMatrixSidePanel, {
      props: {
        panel: {
          label: 'People',
          items: [],
        },
      },
      global: {
        stubs: { UaBtn: true, CalendarMatrixSidePanelItem: true },
      },
    });

    expect(wrapper.text()).toContain('No items to display.');
  });
});
