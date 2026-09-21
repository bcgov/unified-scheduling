import { defineStore } from 'pinia';
import { computed, ref } from 'vue';

export type CalendarAlertSeverity = 'info' | 'warning' | 'error';

export interface CalendarAlert {
  id: string;
  message: string;
  severity: CalendarAlertSeverity;
  source?: string;
  dismissible?: boolean;
}

export const useCalendarAlertStore = defineStore('calendar-alerts', () => {
  const alertList = ref<CalendarAlert[]>([]);

  const alerts = computed(() => alertList.value);

  const setAlert = (alert: CalendarAlert) => {
    const existingIndex = alertList.value.findIndex((candidate) => candidate.id === alert.id);

    if (existingIndex >= 0) {
      alertList.value.splice(existingIndex, 1, alert);
      return;
    }

    alertList.value.push(alert);
  };

  const clearAlert = (id: string) => {
    alertList.value = alertList.value.filter((alert) => alert.id !== id);
  };

  const clearAllAlerts = () => {
    alertList.value = [];
  };

  return {
    alerts,
    setAlert,
    clearAlert,
    clearAllAlerts,
  };
});
