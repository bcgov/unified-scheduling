import { defineStore } from 'pinia';
import { ref } from 'vue';
import { getInitialCalendarDateRange, type CalendarDateRange } from '@/utils/date';

export type CalendarPeriod = 'day' | 'week' | 'work-week' | 'month';

export const useCalendarStore = defineStore('calendar', () => {
  const activeViewId = ref('');
  const dateRange = ref<CalendarDateRange>(getInitialCalendarDateRange());
  const period = ref<CalendarPeriod>('week');
  const locationId = ref<number>();
  const filters = ref<Record<string, unknown>>({});
  const selectedEventId = ref<string>();
  const selectedResourceId = ref<string>();

  const setActiveView = (viewId: string) => {
    activeViewId.value = viewId;
  };

  const setDateRange = (startDate: string, endDate: string) => {
    dateRange.value = { startDate, endDate };
  };

  const setPeriod = (value: CalendarPeriod) => {
    period.value = value;
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

  return {
    activeViewId,
    dateRange,
    period,
    locationId,
    filters,
    selectedEventId,
    selectedResourceId,
    setActiveView,
    setDateRange,
    setPeriod,
    setLocationId,
    setFilter,
    clearFilter,
    setSelectedEvent,
    clearSelectedEvent,
    setSelectedResource,
    clearSelection,
  };
});
