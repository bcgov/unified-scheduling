<script setup lang="ts">
import { provide, ref } from 'vue';
import { storeToRefs } from 'pinia';
import { calendarActionRegistry } from '../../registry/calendarActionRegistry';
import { useCalendarStore } from '../../calendarStore';
import type { CalendarEventBase, CalendarRuntimeContext } from '../../calendarTypes';
import CalendarMatrixGrid from './CalendarMatrixGrid.vue';
import CalendarMatrixSidePanel from './CalendarMatrixSidePanel.vue';
import { calendarMatrixContextKey } from './calendarMatrixContext';
import type {
  CalendarMatrixCellDropEvent,
  CalendarMatrixCellHeaderActionEvent,
  CalendarMatrixCellHeaderClickEvent,
  CalendarMatrixDragPayload,
  CalendarMatrixEventBlockActionEvent,
  CalendarMatrixResource,
  CalendarMatrixViewModel,
} from './calendarMatrixTypes';

const props = withDefaults(
  defineProps<{
    model: CalendarMatrixViewModel;
    runtimeContext?: CalendarRuntimeContext;
  }>(),
  {
    runtimeContext: () => ({ featureFlags: {} }),
  },
);

const emit = defineEmits<{
  (event: 'eventClick', payload: CalendarEventBase): void;
  (event: 'eventAction', payload: CalendarMatrixEventBlockActionEvent): void;
  (event: 'cellDrop', payload: CalendarMatrixCellDropEvent): void;
  (event: 'headerAction', payload: CalendarMatrixCellHeaderActionEvent): void;
  (event: 'headerClick', payload: CalendarMatrixCellHeaderClickEvent): void;
  (event: 'sidePanelAction'): void;
  (event: 'dragStart', payload: CalendarMatrixDragPayload): void;
  (event: 'resourceAdd', payload: CalendarMatrixResource): void;
}>();

const calendarStore = useCalendarStore();
const { selectedEventId, selectedResourceId } = storeToRefs(calendarStore);
const activeDragPayload = ref<CalendarMatrixDragPayload>();

type MatrixActionExecutionOptions<TAction> = {
  actions: TAction[];
  duplicateMessage: string;
  execute: (action: TAction) => Promise<void> | void;
};

provide(calendarMatrixContextKey, {
  selectedEventId,
  selectedResourceId,
  activeDragPayload,
  selectEvent: calendarStore.setSelectedEvent,
  selectResource: calendarStore.setSelectedResource,
  startDrag: (payload) => {
    activeDragPayload.value = payload;
  },
  clearDrag: () => {
    activeDragPayload.value = undefined;
  },
  dropOnCell: () => {
    activeDragPayload.value = undefined;
  },
});

function handleEventClick(event: CalendarEventBase) {
  calendarStore.setSelectedEvent(event.id);
  emit('eventClick', event);
}

async function executeMatrixAction<TAction>({
  actions,
  duplicateMessage,
  execute,
}: MatrixActionExecutionOptions<TAction>) {
  if (actions.length === 0) {
    return;
  }

  if (actions.length > 1) {
    throw new Error(duplicateMessage);
  }

  await execute(actions[0]);
}

async function handleCellDrop(dropEvent: CalendarMatrixCellDropEvent) {
  const actions = calendarActionRegistry.getDropActions(dropEvent.drag, dropEvent.drop, props.runtimeContext);

  await executeMatrixAction({
    actions,
    duplicateMessage: 'Multiple calendar matrix drop actions are available for this drag/drop context.',
    execute: (action) => action.execute(dropEvent.drag, dropEvent.drop, props.runtimeContext),
  });
}

async function handleSidePanelAction() {
  if (!props.model.sidePanel) {
    return;
  }

  const actionContext = {
    panel: props.model.sidePanel,
    actionId: props.model.sidePanel.actionId,
    model: props.model,
  };
  const actions = calendarActionRegistry.getMatrixSidePanelActions(actionContext, props.runtimeContext);

  await executeMatrixAction({
    actions,
    duplicateMessage: `Multiple calendar matrix side panel actions handle '${actionContext.actionId}'.`,
    execute: (action) => action.execute(actionContext, props.runtimeContext),
  });
}

async function handleResourceAdd(resource: CalendarMatrixResource) {
  const actionContext = {
    resource,
    actionId: resource.action?.actionId,
    model: props.model,
  };
  const actions = calendarActionRegistry.getMatrixResourceActions(actionContext, props.runtimeContext);

  await executeMatrixAction({
    actions,
    duplicateMessage: `Multiple calendar matrix resource actions handle '${actionContext.actionId}'.`,
    execute: (action) => action.execute(actionContext, props.runtimeContext),
  });
}

async function handleHeaderClick(payload: CalendarMatrixCellHeaderClickEvent) {
  const actionContext = {
    cell: payload.cell,
    header: payload.header,
    actionId: payload.header.actionId,
    model: props.model,
  };
  const actions = calendarActionRegistry.getMatrixCellHeaderActions(actionContext, props.runtimeContext);

  await executeMatrixAction({
    actions,
    duplicateMessage: `Multiple calendar matrix cell header actions handle '${actionContext.actionId}'.`,
    execute: (action) => action.execute(actionContext, props.runtimeContext),
  });
}

async function handleHeaderAction(payload: CalendarMatrixCellHeaderActionEvent) {
  const actionContext = {
    cell: payload.cell,
    header: payload.header,
    actionId: payload.actionId,
    actionType: payload.actionType,
    model: props.model,
  };
  const actions = calendarActionRegistry.getMatrixCellHeaderActions(actionContext, props.runtimeContext);

  await executeMatrixAction({
    actions,
    duplicateMessage: `Multiple calendar matrix cell header actions handle '${actionContext.actionId}'.`,
    execute: (action) => action.execute(actionContext, props.runtimeContext),
  });
}

async function handleEventAction(payload: CalendarMatrixEventBlockActionEvent) {
  const actionContext = {
    event: payload.event,
    actionId: payload.actionId,
    actionType: payload.actionType,
    model: props.model,
  };
  const actions = calendarActionRegistry.getMatrixEventBlockActions(actionContext, props.runtimeContext);

  await executeMatrixAction({
    actions,
    duplicateMessage: `Multiple calendar matrix event block actions handle '${actionContext.actionId}'.`,
    execute: (action) => action.execute(actionContext, props.runtimeContext),
  });
}
</script>

<template>
  <section class="calendar-matrix-view" :class="{ 'has-side-panel': !!model.sidePanel }">
    <CalendarMatrixGrid
      v-if="!model.unsupportedMessage"
      :model="model"
      @cell-drop="handleCellDrop"
      @drag-start="emit('dragStart', $event)"
      @event-action="handleEventAction"
      @event-click="handleEventClick"
      @header-action="handleHeaderAction"
      @header-click="handleHeaderClick"
      @resource-add="handleResourceAdd"
    >
      <template v-if="$slots['resource-row']" #resource-row="slotProps">
        <slot name="resource-row" v-bind="slotProps" />
      </template>
      <template v-if="$slots['resource-action']" #resource-action="slotProps">
        <slot name="resource-action" v-bind="slotProps" />
      </template>
      <template v-if="$slots.cell" #cell="slotProps">
        <slot name="cell" v-bind="slotProps" />
      </template>
      <template v-if="$slots['event-block']" #event-block="slotProps">
        <slot name="event-block" v-bind="slotProps" />
      </template>
      <template v-if="$slots['cell-header']" #cell-header="slotProps">
        <slot name="cell-header" v-bind="slotProps" />
      </template>
    </CalendarMatrixGrid>
    <div v-else class="calendar-matrix-view__unsupported">
      {{ model.unsupportedMessage }}
    </div>
    <CalendarMatrixSidePanel
      v-if="!model.unsupportedMessage && model.sidePanel"
      :panel="model.sidePanel"
      @action="handleSidePanelAction"
      @item-drag-start="emit('dragStart', $event)"
    >
      <template v-if="$slots['side-panel-item']" #item="slotProps">
        <slot name="side-panel-item" v-bind="slotProps" />
      </template>
    </CalendarMatrixSidePanel>
  </section>
</template>

<style scoped>
.calendar-matrix-view {
  display: grid;
  gap: var(--ua-spacing-md);
  grid-template-columns: minmax(0, 1fr);
  padding: var(--ua-spacing-lg) 0 0;
}

.calendar-matrix-view.has-side-panel {
  grid-template-columns: minmax(0, 1fr) minmax(240px, 300px);
}

.calendar-matrix-view__unsupported {
  background: rgb(var(--v-theme-surface));
  border: 1px solid var(--ua-border-color);
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-base);
  padding: var(--ua-spacing-lg);
}

@media (max-width: 900px) {
  .calendar-matrix-view.has-side-panel {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
