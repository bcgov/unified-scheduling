<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue';
import { useAccessControl } from '@/composables/useAccessControl';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaCard from '@/shared/components/UaCard.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import { formatRangeLabel } from '@/utils/date';
import CalendarEventDetailModal from './components/CalendarEventDetailModal.vue';
import CalendarMatrixSkeleton from './components/matrix/CalendarMatrixSkeleton.vue';
import { useCalendarAlertStore } from './calendarAlertStore';
import { calendarDataService } from './calendarDataService';
import { DEFAULT_CALENDAR_PERIODS } from './calendarPeriodOptions';
import { calendarActionRegistry } from './registry/calendarActionRegistry';
import { calendarRegistry } from './registry/calendarRegistry';
import type { CalendarToolbarButton } from './registry/calendarActionRegistryTypes';
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
const calendarAlertStore = useCalendarAlertStore();
const locationsStore = useLocationsStore();
const { activeViewId, dateRange, anchorDate, period, filters, refreshNonce, selectedEventId } =
  storeToRefs(calendarStore);
const { selectedLocationId } = storeToRefs(locationsStore);

const activeLocationId = computed<number | undefined>(() => {
  const candidate = selectedLocationId.value;

  if (candidate === '' || candidate == null) {
    return undefined;
  }

  const parsedLocationId = Number(candidate);
  return Number.isFinite(parsedLocationId) ? parsedLocationId : undefined;
});

const activeLocationTimeZone = computed(() => {
  const locationId = activeLocationId.value;
  return locationId == null ? undefined : locationsStore.entitiesMap[locationId]?.timezone;
});

const runtimeContext = computed<CalendarRuntimeContext>(() => ({
  featureFlags: accessControl.configStore.config?.featureFlags ?? {},
  permissions: accessControl.authStore.userInfo?.permissions ?? [],
}));

const views = computed(() => calendarRegistry.getAvailableViews(runtimeContext.value));
const loading = ref(false);
const errorMessage = ref('');
const dataResponse = ref<CalendarDataResponse>({ contributions: {} });
const loadedContextKey = ref<string>();
const calendarPage = ref<HTMLElement>();
const LOCATION_REQUIRED_ALERT_ID = 'calendar.location.required';

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

const activePeriods = computed<readonly CalendarPeriod[]>(() => {
  const supportedPeriods = activeView.value?.supportedPeriods;

  return supportedPeriods?.length ? supportedPeriods : DEFAULT_CALENDAR_PERIODS;
});

watch(
  activePeriods,
  (periods) => {
    const fallbackPeriod = periods[0] ?? DEFAULT_CALENDAR_PERIODS[0];

    if (!periods.includes(period.value)) {
      calendarStore.setPeriod(fallbackPeriod);
    }
  },
  { immediate: true },
);

const queryContext = computed<CalendarQueryContext>(() => {
  const effectiveFilters = { ...filters.value };
  if (activeLocationTimeZone.value) {
    effectiveFilters.timeZoneId = activeLocationTimeZone.value;
  }

  return {
    startDate: dateRange.value.startDate,
    endDate: dateRange.value.endDate,
    locationId: activeLocationId.value,
    filters: effectiveFilters,
  };
});

const createActionContext = () => ({
  startDate: queryContext.value.startDate,
  endDate: queryContext.value.endDate,
  activeViewId: activeView.value?.id,
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

const registeredToolbarActions = computed(() => {
  if (!activeView.value) {
    return [];
  }

  return calendarActionRegistry.getToolbarActionsForView(activeView.value.id, queryContext.value, runtimeContext.value);
});

const toolbarActions = computed<CalendarToolbarButton[]>(() =>
  registeredToolbarActions.value.map(({ id, label, disabled, variant }) => ({ id, label, disabled, variant })),
);

const createActions = computed<CalendarToolbarButton[]>(() => {
  const actions = calendarActionRegistry.getCreateActions(createActionContext(), runtimeContext.value);

  return actions.map((action) => ({
    id: action.id,
    label: action.label,
    disabled: action.disabled,
    variant: 'outlined' as const,
    prompt: hasCurrentData.value
      ? action.getPrompt?.(dataResponse.value, createActionContext(), runtimeContext.value)
      : undefined,
  }));
});

const rangeLabel = computed(() =>
  formatRangeLabel(queryContext.value.startDate, queryContext.value.endDate, period.value),
);
const runtimeContextKey = computed(() => JSON.stringify(runtimeContext.value));
const dataContextKey = computed(() =>
  JSON.stringify({
    startDate: dateRange.value.startDate,
    endDate: dateRange.value.endDate,
    period: period.value,
    locationId: activeLocationId.value ?? null,
    filters: queryContext.value.filters,
    runtimeContextKey: runtimeContextKey.value,
  }),
);
const reloadKey = computed(() => JSON.stringify({ context: dataContextKey.value, refreshNonce: refreshNonce.value }));
const hasCurrentData = computed(() => loadedContextKey.value === dataContextKey.value);

let panelScrollContext = {
  viewId: activeViewId.value,
  locationId: activeLocationId.value,
  generation: calendarStore.scrollGeneration,
};
let panelScrollCount = 0;

function getPanel() {
  return calendarPage.value?.querySelector<HTMLElement>('.calendar-page__panel');
}

function savePanelScroll() {
  const panel = getPanel();
  if (!panel) return;
  calendarStore.savePanelScrollPosition(
    panelScrollContext.viewId,
    panelScrollContext.locationId,
    panelScrollContext.generation,
    panel.scrollTop,
  );
}

function handlePanelScroll(event: Event) {
  if (event.target === getPanel()) {
    panelScrollCount += 1;
    savePanelScroll();
  }
}

function restorePanelScroll() {
  const panel = getPanel();
  if (!panel) return;
  const { panelTop } = calendarStore.getScrollPosition(panelScrollContext.viewId, panelScrollContext.locationId);
  panel.scrollTop = Math.min(Math.max(0, panelTop), Math.max(0, panel.scrollHeight - panel.clientHeight));
  savePanelScroll();
}

watch(
  [() => dateRange.value.startDate, () => dateRange.value.endDate, period, activeLocationId],
  () => {
    calendarStore.resetScrollPositions();
    panelScrollContext = {
      viewId: activeViewId.value,
      locationId: activeLocationId.value,
      generation: calendarStore.scrollGeneration,
    };
    const panel = getPanel();
    if (panel) {
      panel.scrollTop = 0;
      panel.scrollLeft = 0;
    }
  },
  { flush: 'sync' },
);

watch(activeViewId, (viewId) => {
  savePanelScroll();
  const generation = calendarStore.scrollGeneration;
  const locationId = activeLocationId.value;
  const scrollCount = panelScrollCount;
  void nextTick().then(() => {
    if (generation !== calendarStore.scrollGeneration || viewId !== activeViewId.value) return;
    panelScrollContext = { viewId, locationId, generation };
    if (panelScrollCount === scrollCount) restorePanelScroll();
    else savePanelScroll();
  });
});

watch(dataResponse, () => {
  void nextTick().then(() => {
    const panel = getPanel();
    if (!panel || !hasCurrentData.value) return;
    panel.scrollTop = Math.min(panel.scrollTop, Math.max(0, panel.scrollHeight - panel.clientHeight));
    savePanelScroll();
  });
});

const loadData = async () => {
  const requestId = ++latestRequestId;
  const requestContextKey = dataContextKey.value;

  if (activeLocationId.value === undefined) {
    calendarDataService.cancel();
    loading.value = false;
    errorMessage.value = '';
    dataResponse.value = { contributions: {} };
    loadedContextKey.value = undefined;
    calendarStore.clearSelectedEvent();
    return;
  }

  loading.value = true;
  errorMessage.value = '';

  try {
    const response = await calendarDataService.loadData(runtimeContext.value, queryContext.value, calendarRegistry);
    if (requestId === latestRequestId && requestContextKey === dataContextKey.value) {
      dataResponse.value = response;
      loadedContextKey.value = requestContextKey;
    }
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      return;
    }

    if (requestId === latestRequestId) {
      errorMessage.value = 'Failed to retrieve calendar data.';
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

watch(
  activeLocationId,
  (locationId) => {
    if (locationId === undefined) {
      calendarAlertStore.setAlert({
        id: LOCATION_REQUIRED_ALERT_ID,
        severity: 'warning',
        message: 'Please select a location.',
        source: 'calendar',
      });
      return;
    }

    calendarAlertStore.clearAlert(LOCATION_REQUIRED_ALERT_ID);
  },
  { immediate: true },
);

watch(selectedDetailEvent, (event) => {
  if (!event && selectedEventId.value) {
    calendarStore.clearSelectedEvent();
  }
});

onBeforeUnmount(() => {
  calendarDataService.endSession();
});

const handlePrevious = () => calendarStore.shiftPeriod('previous');

const handleNext = () => calendarStore.shiftPeriod('next');

const handleToday = () => calendarStore.goToToday();
const handleRefresh = () => calendarStore.refresh();

const handleDateSelection = (selectedDate: string) => calendarStore.setAnchorDate(selectedDate);

const handlePeriodChange = (nextPeriod: CalendarPeriod) => calendarStore.setPeriod(nextPeriod);

const handleToolbarAction = async (actionId: string) => {
  const action = registeredToolbarActions.value.find((candidate) => candidate.id === actionId);

  if (!action || action.disabled) {
    return;
  }

  await action.run(queryContext.value, runtimeContext.value);
};

const handleCreateAction = async (actionId: string) => {
  const action = calendarActionRegistry
    .getCreateActions(createActionContext(), runtimeContext.value)
    .find((candidate) => candidate.id === actionId);

  if (!action?.run || action.disabled) {
    return;
  }

  await action.run(createActionContext(), runtimeContext.value);
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
  const actions = calendarActionRegistry.getViewDetailActions(activeView.value.id, actionContext);

  if (actions.length === 0) {
    return;
  }

  for (const action of actions) {
    await action.run(actionContext);
  }
};
</script>

<template>
  <section ref="calendarPage" class="calendar-page">
    <div v-if="calendarAlertStore.alerts.length" class="calendar-alerts">
      <UaAlert
        v-for="alert in calendarAlertStore.alerts"
        :key="alert.id"
        :type="alert.severity"
        :closable="alert.dismissible !== false"
        @close="calendarAlertStore.clearAlert(alert.id)"
      >
        {{ alert.message }}
      </UaAlert>
    </div>

    <UaAlert v-if="errorMessage" type="error" @close="errorMessage = ''">
      {{ errorMessage }}
    </UaAlert>

    <UaCard
      class="calendar-page__panel"
      :class="{ 'calendar-page__panel--bounded': activeView?.fillAvailableHeight }"
      body-padding="none"
      @scroll.capture.passive="handlePanelScroll"
    >
      <CalendarToolbar
        :views="views"
        :active-view-id="activeViewId"
        :create-actions="createActions"
        :range-label="rangeLabel"
        :anchor-date="anchorDate"
        :toolbar-actions="toolbarActions"
        :active-period="period"
        :periods="activePeriods"
        :is-loading="loading"
        @update:active-view-id="handleActiveViewChange"
        @update:period="handlePeriodChange"
        @select-date="handleDateSelection"
        @previous="handlePrevious"
        @next="handleNext"
        @today="handleToday"
        @refresh="handleRefresh"
        @trigger-toolbar-action="handleToolbarAction"
        @trigger-create-action="handleCreateAction"
      />

      <CalendarMatrixSkeleton
        v-if="activeLocationId !== undefined && loading && !hasCurrentData && activeView?.loadingVariant === 'matrix'"
      />
      <div v-else-if="activeLocationId !== undefined && loading && !hasCurrentData" class="calendar-page__state">
        Loading calendar...
      </div>
      <div
        v-else-if="activeLocationId !== undefined && hasCurrentData && activeView && activeViewModel"
        class="calendar-page__view"
      >
        <component
          :is="activeView.component"
          :model="activeViewModel"
          :runtime-context="runtimeContext"
          @event-click="handleViewEventClick"
        />
      </div>
      <div v-else-if="activeLocationId !== undefined && activeView && errorMessage" class="calendar-page__state" />
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
  max-height: calc(100vh - var(--ua-appbar-height) - var(--ua-spacing-md) - var(--ua-spacing-xl));
  overflow: auto;
}

.calendar-alerts {
  display: grid;
  gap: var(--ua-spacing-sm);
  margin-bottom: var(--ua-spacing-md);
}

.calendar-page__panel :deep(.calendar-toolbar-shell) {
  position: sticky;
  top: 0;
  z-index: 5;
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

@media (min-width: 901px) {
  .calendar-page__panel--bounded {
    height: calc(100vh - var(--ua-appbar-height) - var(--ua-spacing-md) - var(--ua-spacing-xl));
    min-height: 320px;
    overflow: hidden;
  }

  .calendar-page__panel--bounded :deep(.ua-card__body) {
    display: flex;
    flex-direction: column;
    height: 100%;
    min-height: 0;
  }

  .calendar-page__panel--bounded :deep(.calendar-toolbar-shell) {
    flex-shrink: 0;
  }

  .calendar-page__panel--bounded .calendar-page__view {
    display: flex;
    flex: 1 1 auto;
    flex-direction: column;
    min-height: 0;
  }
}
</style>
