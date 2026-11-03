import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import {
  buildDateRangeForPeriod,
  getTodayDateOnly,
  shiftCalendarAnchor,
  type CalendarNavigationDirection,
} from '@/utils/date';

export type CalendarPeriod = 'day' | 'week' | 'work-week' | 'month';

export const useCalendarStore = defineStore('calendar', () => {
  const activeViewId = ref('');
  const anchorDate = ref(getTodayDateOnly());
  const hasExplicitAnchor = ref(false);
  const period = ref<CalendarPeriod>('week');
  const dateRange = computed(() => buildDateRangeForPeriod(anchorDate.value, period.value));
  const locationId = ref<number>();
  const filters = ref<Record<string, unknown>>({});
  const refreshNonce = ref(0);
  const selectedEventId = ref<string>();
  const selectedResourceId = ref<string>();

  const setActiveView = (viewId: string) => {
    activeViewId.value = viewId;
  };

  const setAnchorDate = (value: string) => {
    hasExplicitAnchor.value = true;
    anchorDate.value = value;
  };

  const initializeAnchorDate = (timeZoneId?: string) => {
    if (!hasExplicitAnchor.value) {
      anchorDate.value = getTodayDateOnly(timeZoneId);
    }
  };

  const setPeriod = (value: CalendarPeriod) => {
    period.value = value;
  };

  const shiftPeriod = (direction: CalendarNavigationDirection) => {
    hasExplicitAnchor.value = true;
    anchorDate.value = shiftCalendarAnchor(anchorDate.value, period.value, direction);
  };

  const goToToday = (timeZoneId?: string) => {
    anchorDate.value = getTodayDateOnly(timeZoneId);
    hasExplicitAnchor.value = false;
  };

  const setLocationId = (value?: number) => {
    locationId.value = value ?? undefined;
  };

  const setFilter = (key: string, value: unknown) => {
    filters.value = {
      ...filters.value,
      [key]: value,
    };
  };

  const clearFilter = (key: string) => {
    const nextFilters = { ...filters.value };
    delete nextFilters[key];
    filters.value = nextFilters;
  };

  const setSelectedEvent = (eventId?: string) => {
    selectedEventId.value = eventId;
  };

  const clearSelectedEvent = () => {
    selectedEventId.value = undefined;
  };

  const setSelectedResource = (resourceId?: string) => {
    selectedResourceId.value = resourceId;
  };

  const clearSelection = () => {
    selectedEventId.value = undefined;
    selectedResourceId.value = undefined;
  };

  const refresh = () => {
    refreshNonce.value += 1;
  };

  return {
    activeViewId,
    dateRange,
    anchorDate,
    hasExplicitAnchor,
    period,
    locationId,
    filters,
    refreshNonce,
    selectedEventId,
    selectedResourceId,
    setActiveView,
    setAnchorDate,
    initializeAnchorDate,
    setPeriod,
    shiftPeriod,
    goToToday,
    setLocationId,
    setFilter,
    clearFilter,
    setSelectedEvent,
    clearSelectedEvent,
    setSelectedResource,
    clearSelection,
    refresh,
  };
});
