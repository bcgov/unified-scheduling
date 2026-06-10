<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { useAccessControl } from '@/composables/useAccessControl';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaCard from '@/shared/components/UaCard.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import type { SelectOption, SelectValue } from '@/types/select';
import CalendarEventDetailModal from './components/CalendarEventDetailModal.vue';
import { calendarDataService } from './calendarDataService';
import { buildDateRangeForPeriod, formatRangeLabel, getTodayDateOnly, shiftDateRange } from './calendarDateUtils';
import { calendarRegistry } from './registry/calendarRegistry';
import { calendarActionRegistry } from './registry/calendarActionRegistry';
import type { CalendarToolbarAction } from './registry/calendarRegistryTypes';
import { type CalendarPeriod, useCalendarStore } from './calendarStore';
import type {
  CalendarDataResponse,
  CalendarEventBase,
  CalendarQueryContext,
  CalendarRuntimeContext,
} from './calendarTypes';
import { selectCalendarEvents } from './calendarSelectors';
import CalendarToolbar from './components/CalendarToolbar.vue';

const accessControl = useAccessControl();
const calendarStore = useCalendarStore();
const locationsStore = useLocationsStore();
const { activeViewId, dateRange, period, locationId, filters, selectedEventId } = storeToRefs(calendarStore);

const runtimeContext = computed<CalendarRuntimeContext>(() => ({
  featureFlags: accessControl.configStore.config?.featureFlags ?? {},
}));

const views = computed(() => calendarRegistry.getAvailableViews(runtimeContext.value));
const loading = ref(false);
const errorMessage = ref('');
const dataResponse = ref<CalendarDataResponse>({ contributions: {} });

let latestRequestId = 0;

watch(
  views,
  (availableViews) => {
    const activeViewExists = availableViews.some((view) => view.id === activeViewId.value);

    if ((!activeViewId.value || !activeViewExists) && availableViews.length > 0) {
      calendarStore.setActiveView(availableViews[0].id);
    }
  },
  { immediate: true },
);

const activeView = computed(() => {
  return views.value.find((view) => view.id === activeViewId.value) ?? views.value[0];
});

const queryContext = computed<CalendarQueryContext>(() => {
  return {
    startDate: dateRange.value.startDate,
    endDate: dateRange.value.endDate,
    locationId: locationId.value ? Number(locationId.value) : undefined,
    filters: { ...filters.value },
  };
});

const createActionContext = () => ({
  startDate: queryContext.value.startDate,
  endDate: queryContext.value.endDate,
  locationId: queryContext.value.locationId,
  filters: queryContext.value.filters,
});

const activeViewModel = computed(() => {
  if (!activeView.value) {
    return null;
  }

  return activeView.value.buildModel(dataResponse.value, queryContext.value, runtimeContext.value, period.value);
});

const calendarEvents = computed(() => selectCalendarEvents(dataResponse.value));

const selectedDetailEvent = computed(() => {
  if (!selectedEventId.value) {
    return undefined;
  }

  return calendarEvents.value.find((event) => event.id === selectedEventId.value);
});

const locationOptions = computed<SelectOption[]>(() => {
  return [
    { code: 'all', description: 'All locations' },
    ...locationsStore.getSelectOptions().map((option) => ({
      ...option,
      code: String(option.code),
    })),
  ];
});

const locationValue = computed<SelectValue>({
  get: () => locationId.value ?? 'all',
  set: (value) => {
    calendarStore.setLocationId(typeof value === 'string' && value !== 'all' ? value : undefined);
  },
});

const toolbarActions = computed<CalendarToolbarAction[]>(() => {
  return activeView.value ? calendarRegistry.getToolbarActionsForView(activeView.value.id, queryContext.value) : [];
});

const createActions = computed<CalendarToolbarAction[]>(() => {
  const actions = calendarActionRegistry.getCreateActions(createActionContext(), runtimeContext.value);

  return actions.map((action) => ({
    id: action.id,
    label: action.label,
    disabled: action.disabled,
    variant: 'outlined' as const,
    onClick: action.run ? () => action.run?.(createActionContext()) : undefined,
  }));
});

const rangeLabel = computed(() =>
  formatRangeLabel(queryContext.value.startDate, queryContext.value.endDate, period.value),
);
const runtimeContextKey = computed(() => JSON.stringify(accessControl.configStore.config?.featureFlags ?? {}));
const reloadKey = computed(() =>
  JSON.stringify({
    startDate: dateRange.value.startDate,
    endDate: dateRange.value.endDate,
    period: period.value,
    locationId: locationId.value ?? null,
    filters: filters.value,
    runtimeContextKey: runtimeContextKey.value,
  }),
);

const loadData = async () => {
  const requestId = ++latestRequestId;
  loading.value = true;
  errorMessage.value = '';

  try {
    dataResponse.value = await calendarDataService.loadData(runtimeContext.value, queryContext.value, calendarRegistry);
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      return;
    }

    if (requestId === latestRequestId) {
      errorMessage.value = error instanceof Error ? error.message : 'Failed to load calendar data.';
    }
  } finally {
    if (requestId === latestRequestId) {
      loading.value = false;
    }
  }
};

watch(
  reloadKey,
  () => {
    void loadData();
  },
  { immediate: true },
);

watch(selectedDetailEvent, (event) => {
  if (!event && selectedEventId.value) {
    calendarStore.clearSelectedEvent();
  }
});

onBeforeUnmount(() => {
  calendarDataService.cancel();
});

const handlePrevious = () => {
  const nextRange = shiftDateRange(dateRange.value.startDate, period.value, -1);
  calendarStore.setDateRange(nextRange.startDate, nextRange.endDate);
};

const handleNext = () => {
  const nextRange = shiftDateRange(dateRange.value.startDate, period.value, 1);
  calendarStore.setDateRange(nextRange.startDate, nextRange.endDate);
};

const handleToday = () => {
  const nextRange = buildDateRangeForPeriod(getTodayDateOnly(), period.value);
  calendarStore.setDateRange(nextRange.startDate, nextRange.endDate);
};

const handlePeriodChange = (nextPeriod: CalendarPeriod) => {
  calendarStore.setPeriod(nextPeriod);

  const anchorDate = dateRange.value.startDate;
  const nextRange = buildDateRangeForPeriod(anchorDate, nextPeriod);
  calendarStore.setDateRange(nextRange.startDate, nextRange.endDate);
};

const handleToolbarAction = async (actionId: string) => {
  const action = toolbarActions.value.find((candidate) => candidate.id === actionId);

  if (!action?.onClick || action.disabled) {
    return;
  }

  await action.onClick();
};

const handleCreateAction = async (actionId: string) => {
  const action = createActions.value.find((candidate) => candidate.id === actionId);

  if (!action?.onClick || action.disabled) {
    return;
  }

  await action.onClick();
};

const handleActiveViewChange = (viewId: string) => {
  calendarStore.setActiveView(viewId);
};

const handleViewEventClick = async (event: CalendarEventBase) => {
  if (!activeView.value) {
    return;
  }

  const actionContext = {
    event,
    viewId: activeView.value.id,
    queryContext: queryContext.value,
    runtimeContext: runtimeContext.value,
  };
  const actions = calendarRegistry.getViewDetailActions(activeView.value.id, actionContext);

  if (actions.length === 0) {
    return;
  }

  for (const action of actions) {
    await action.run(actionContext);
  }
};
</script>

<template>
  <section class="calendar-page">
    <UaAlert v-if="errorMessage" type="error" @close="errorMessage = ''">
      {{ errorMessage }}
    </UaAlert>

    <UaCard class="calendar-page__panel" body-padding="none">
      <CalendarToolbar
        :views="views"
        :active-view-id="activeViewId"
        :create-actions="createActions"
        :location-options="locationOptions"
        :location-value="locationValue"
        :range-label="rangeLabel"
        :toolbar-actions="toolbarActions"
        :active-period="period"
        :is-loading="loading"
        @update:active-view-id="handleActiveViewChange"
        @update:location-value="locationValue = $event ?? 'all'"
        @update:period="handlePeriodChange"
        @previous="handlePrevious"
        @next="handleNext"
        @today="handleToday"
        @trigger-toolbar-action="handleToolbarAction"
        @trigger-create-action="handleCreateAction"
      />

      <div v-if="loading" class="calendar-page__state">Loading calendar…</div>
      <div v-else-if="activeView && activeViewModel" class="calendar-page__view">
        <component :is="activeView.component" :model="activeViewModel" @event-click="handleViewEventClick" />
      </div>
      <div v-else class="calendar-page__state">No calendar views are registered.</div>
    </UaCard>

    <CalendarEventDetailModal
      v-if="selectedDetailEvent"
      :event="selectedDetailEvent"
      @close="calendarStore.clearSelectedEvent()"
    />
  </section>
</template>

<style scoped>
.calendar-page {
  padding-bottom: var(--ua-spacing-xl);
}

.calendar-page__panel {
  background: var(--ua-calendar-panel-bg);
  border-radius: 0;
}

.calendar-page__state {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-base);
  padding: var(--ua-spacing-lg) var(--ua-spacing-xl) var(--ua-spacing-xl);
}

.calendar-page__view {
  background: var(--ua-calendar-panel-bg);
  min-height: 320px;
  padding: 0 0 var(--ua-spacing-xl);
}
</style>
