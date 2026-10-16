<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { DateTime } from 'luxon';
import { computed, nextTick, onBeforeUnmount, onErrorCaptured, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { useAccessControl } from '@/composables/useAccessControl';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaCard from '@/shared/components/UaCard.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import {
  addDays,
  buildDateRangeForPeriod,
  formatCalendarDateOnly,
  formatDateTime,
  formatRangeLabel,
} from '@/utils/date';
import CalendarEventDetailModal from './components/CalendarEventDetailModal.vue';
import CalendarMatrixSkeleton from './components/matrix/CalendarMatrixSkeleton.vue';
import { useCalendarAlertStore } from './calendarAlertStore';
import { calendarDataService } from './calendarDataService';
import { DEFAULT_CALENDAR_PERIODS, isCalendarPeriod } from './calendarPeriodOptions';
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
const route = useRoute();
const calendarStore = useCalendarStore();
const calendarAlertStore = useCalendarAlertStore();
const locationsStore = useLocationsStore();
const { activeViewId, dateRange, anchorDate, period, filters, refreshNonce, selectedEventId } =
  storeToRefs(calendarStore);
const { selectedLocationId } = storeToRefs(locationsStore);
const scheduleViewId = 'calendar.matrix-schedule';
const assignmentViewId = 'calendar.matrix-assignment';
const viewIdByRouteView: Record<string, string> = {
  schedule: scheduleViewId,
  assignments: assignmentViewId,
};
const isPrintMode = computed(() => route.query.mode === 'print');

interface CalendarPrintContext {
  viewId: string;
  locationId: number;
  startDate: string;
  endDate: string;
  period: CalendarPeriod;
}

const printContext = computed<CalendarPrintContext | undefined>(() => {
  if (!isPrintMode.value) {
    return undefined;
  }

  const viewId = viewIdByRouteView[readQueryValue(route.query.view) ?? ''];
  const locationId = parsePositiveInteger(readQueryValue(route.query.locationId));
  const startDate = readQueryValue(route.query.startDate);
  const endDate = readQueryValue(route.query.endDate);
  const requestedPeriod = readQueryValue(route.query.period);
  const period = isCalendarPeriod(requestedPeriod) && requestedPeriod !== 'month' ? requestedPeriod : undefined;

  if (
    !viewId ||
    !locationId ||
    !isValidDateOnly(startDate) ||
    !isValidDateOnly(endDate) ||
    !period ||
    buildDateRangeForPeriod(startDate, period).endDate !== endDate
  ) {
    return undefined;
  }

  return { viewId, locationId, startDate, endDate, period };
});

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
  presentationMode: isPrintMode.value ? 'print' : 'interactive',
}));

const views = computed(() => calendarRegistry.getAvailableViews(runtimeContext.value));
const loading = ref(false);
const hasLoadedData = ref(false);
const printGeneratedAt = ref('');
const activePrintRenderToken = ref('');
const completedPrintRenderToken = ref('');
const errorMessage = ref('');
const dataResponse = ref<CalendarDataResponse>({ contributions: {} });
const LOCATION_REQUIRED_ALERT_ID = 'calendar.location.required';

let latestRequestId = 0;

watch(
  [views, printContext],
  ([availableViews, requestedPrintContext]) => {
    const requestedViewId = requestedPrintContext?.viewId;
    if (requestedViewId && availableViews.some((view) => view.id === requestedViewId)) {
      if (activeViewId.value !== requestedViewId) {
        calendarStore.setActiveView(requestedViewId);
      }
      return;
    }

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
    activeViewId: activeView.value?.id,
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

const isPrintContextResolved = computed(() => {
  const context = printContext.value;
  return (
    context != null &&
    activeView.value?.id === context.viewId &&
    activeLocationId.value === context.locationId &&
    queryContext.value.startDate === context.startDate &&
    queryContext.value.endDate === context.endDate &&
    period.value === context.period
  );
});

const printContextError = computed(() => {
  if (!isPrintMode.value) {
    return undefined;
  }
  if (!printContext.value) {
    return 'The calendar print context is invalid.';
  }
  if (!views.value.some((view) => view.id === printContext.value?.viewId)) {
    return 'The requested calendar print view is unavailable.';
  }
  if (!loading.value && hasLoadedData.value && isUnsupportedPrintViewModel(activeViewModel.value)) {
    return 'The requested calendar print view is unsupported.';
  }
  return errorMessage.value || undefined;
});

const printRenderState = computed(() => {
  if (printContextError.value) {
    return 'error';
  }

  if (
    !isPrintMode.value ||
    loading.value ||
    !hasLoadedData.value ||
    !printGeneratedAt.value ||
    !activeView.value ||
    !activeViewModel.value ||
    isUnsupportedPrintViewModel(activeViewModel.value) ||
    !isPrintContextResolved.value ||
    !activePrintRenderToken.value ||
    completedPrintRenderToken.value !== activePrintRenderToken.value
  ) {
    return 'loading';
  }

  return 'complete';
});
const hasTriggeredPrintDialog = ref(false);

watch(
  [printRenderState, () => route.query.printDialog],
  async ([renderState, printDialog]) => {
    if (renderState !== 'complete' || printDialog !== '1' || hasTriggeredPrintDialog.value) {
      return;
    }

    hasTriggeredPrintDialog.value = true;
    await nextTick();
    await document.fonts?.ready;
    await waitForAnimationFrame();
    await waitForAnimationFrame();
    window.print();
  },
  { immediate: true },
);

onErrorCaptured(() => {
  if (!isPrintMode.value) {
    return;
  }

  errorMessage.value = 'Failed to render the calendar print view.';
  return false;
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
  }));
});

const rangeLabel = computed(() =>
  formatRangeLabel(queryContext.value.startDate, queryContext.value.endDate, period.value),
);
const printLocationHeading = computed(() => {
  const locationId = activeLocationId.value;
  return locationId ? (locationsStore.entitiesMap[locationId]?.name ?? 'Unknown location') : 'Unknown location';
});
const printDateRange = computed(() => {
  const startDate = formatCalendarDateOnly(queryContext.value.startDate);
  const endDate = formatCalendarDateOnly(addDays(queryContext.value.endDate, -1));
  return `${startDate} - ${endDate}`;
});
const runtimeContextKey = computed(() => JSON.stringify(runtimeContext.value));
const reloadKey = computed(() =>
  JSON.stringify({
    activeViewId: activeView.value?.id ?? null,
    startDate: dateRange.value.startDate,
    endDate: dateRange.value.endDate,
    period: period.value,
    locationId: activeLocationId.value ?? null,
    filters: queryContext.value.filters,
    refreshNonce: refreshNonce.value,
    runtimeContextKey: runtimeContextKey.value,
  }),
);

watch(
  printContext,
  (context) => {
    if (!context) {
      return;
    }

    calendarStore.setPeriod(context.period);
    calendarStore.setAnchorDate(context.startDate);
    locationsStore.setSelectedLocationId(context.locationId);
  },
  { immediate: true },
);

const loadData = async () => {
  const requestId = ++latestRequestId;
  completedPrintRenderToken.value = '';

  if (activeLocationId.value === undefined) {
    calendarDataService.cancel();
    loading.value = false;
    errorMessage.value = '';
    dataResponse.value = { contributions: {} };
    hasLoadedData.value = false;
    printGeneratedAt.value = '';
    activePrintRenderToken.value = '';
    calendarStore.clearSelectedEvent();
    return;
  }

  loading.value = true;
  hasLoadedData.value = false;
  printGeneratedAt.value = '';
  activePrintRenderToken.value = isPrintMode.value ? `${reloadKey.value}:${requestId}` : '';
  errorMessage.value = '';

  try {
    dataResponse.value = await calendarDataService.loadData(runtimeContext.value, queryContext.value, calendarRegistry);
    if (requestId === latestRequestId) {
      hasLoadedData.value = true;
      if (isPrintMode.value) {
        const generatedAt = DateTime.now().setZone(resolvePrintTimeZone());
        printGeneratedAt.value = `${formatDateTime(generatedAt, 'en-CA', {
          month: 'long',
          day: 'numeric',
          year: 'numeric',
          hour: 'numeric',
          minute: '2-digit',
        })} ${generatedAt.offsetNameShort ?? generatedAt.zoneName}`;
      }
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

function readQueryValue(value: unknown) {
  return typeof value === 'string' ? value : undefined;
}

function parsePositiveInteger(value?: string) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function isValidDateOnly(value?: string): value is string {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return false;
  }

  return DateTime.fromISO(value).isValid;
}

function isUnsupportedPrintViewModel(value: unknown) {
  return Boolean(value && typeof value === 'object' && 'unsupportedMessage' in value && value.unsupportedMessage);
}

function resolvePrintTimeZone() {
  const model = activeViewModel.value;
  if (model && typeof model === 'object' && 'timeZone' in model && typeof model.timeZone === 'string') {
    return model.timeZone;
  }
  return activeLocationTimeZone.value;
}

function handlePrintRenderComplete(renderToken?: string) {
  if (isPrintMode.value && renderToken === activePrintRenderToken.value) {
    completedPrintRenderToken.value = renderToken;
  }
}

function waitForAnimationFrame() {
  return new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));
}
</script>

<template>
  <section
    id="calendar-print-root"
    class="calendar-page"
    :class="{ 'calendar-page--print': isPrintMode }"
    :data-render-state="printRenderState"
  >
    <div v-if="!isPrintMode && calendarAlertStore.alerts.length" class="calendar-alerts">
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

    <UaCard class="calendar-page__panel" body-padding="none">
      <CalendarToolbar
        v-if="!isPrintMode"
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
        @trigger-toolbar-action="handleToolbarAction"
        @trigger-create-action="handleCreateAction"
      />

      <header v-if="isPrintMode" class="calendar-page__print-heading">
        <p class="calendar-page__print-location">{{ printLocationHeading }}:</p>
        <h1>{{ printDateRange }}</h1>
        <p class="calendar-page__print-summary">Summary as of {{ printGeneratedAt }}</p>
      </header>

      <CalendarMatrixSkeleton
        v-if="activeLocationId !== undefined && loading && activeView?.loadingVariant === 'matrix'"
      />
      <div v-else-if="activeLocationId !== undefined && loading" class="calendar-page__state">Loading calendar...</div>
      <div v-else-if="activeLocationId !== undefined && activeView && activeViewModel" class="calendar-page__view">
        <component
          :is="activeView.component"
          :model="activeViewModel"
          :period="period"
          :render-token="activePrintRenderToken"
          :runtime-context="runtimeContext"
          @event-click="handleViewEventClick"
          @render-complete="handlePrintRenderComplete"
        />
      </div>
      <div v-else class="calendar-page__state">No calendar views are registered.</div>
    </UaCard>

    <CalendarEventDetailModal
      v-if="!isPrintMode && selectedDetailEvent"
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

.calendar-page__print-heading {
  border: 1px solid var(--ua-text-primary);
  border-radius: var(--ua-border-radius-sm);
  box-sizing: border-box;
  margin: var(--ua-spacing-lg) auto var(--ua-spacing-xl);
  padding: var(--ua-spacing-sm) var(--ua-spacing-xl);
  width: min(28rem, calc(100% - var(--ua-spacing-xl)));
}

.calendar-page__print-location {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  margin: 0;
}

.calendar-page__print-heading h1 {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-md);
  font-weight: var(--ua-font-weight-bold);
  margin: var(--ua-spacing-xs) 0 0;
}

.calendar-page__print-summary {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  font-style: italic;
  margin: var(--ua-spacing-xs) 0 0;
}

.calendar-page--print {
  padding: 0;
  print-color-adjust: exact;
  -webkit-print-color-adjust: exact;
}

.calendar-page--print .calendar-page__panel {
  max-height: none;
  overflow: visible;
}

.calendar-page--print .calendar-page__view {
  padding-bottom: 0;
  pointer-events: none;
}

.calendar-page--print :deep(.calendar-matrix-view) {
  padding-top: 0;
}

.calendar-page--print :deep(.calendar-matrix-grid__scroller) {
  overflow: visible;
}

.calendar-page--print :deep(.calendar-matrix-side-panel),
.calendar-page--print :deep(.calendar-matrix-side-panel__list) {
  max-height: none;
  overflow: visible;
}

.calendar-page--print :deep(.calendar-matrix-event-block__action),
.calendar-page--print :deep(.calendar-matrix-cell-header__action),
.calendar-page--print :deep(.calendar-matrix-cell__empty-add),
.calendar-page--print :deep(.calendar-matrix-resource-row__add),
.calendar-page--print :deep(.calendar-matrix-side-panel__header .v-btn) {
  display: none !important;
}

.calendar-page--print :deep([draggable='true']) {
  cursor: default !important;
}

.calendar-page--print :deep(.calendar-matrix-event-block__meta),
.calendar-page--print :deep(.calendar-matrix-cell-header__text) {
  overflow: visible;
  text-overflow: clip;
  white-space: normal;
}

@media print {
  @page {
    size: letter landscape;
    margin: 0.25in;
  }

  .calendar-page--print :deep(.calendar-matrix-grid) {
    --calendar-matrix-resource-column-min-width: 1.5in;
    --calendar-matrix-resource-column-max-width: 1.5in;
    --calendar-matrix-day-column-min-width: 0.8in;
    font-size: 8pt;
    width: 100%;
  }

  .calendar-page--print :deep(.calendar-matrix-grid__table) {
    min-width: 0;
    width: 100%;
  }

  .calendar-page--print :deep(.calendar-matrix-resource-row),
  .calendar-page--print :deep(.calendar-matrix-cell) {
    break-inside: avoid-page;
  }
}
</style>
