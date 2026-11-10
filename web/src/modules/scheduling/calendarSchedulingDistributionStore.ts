import { defineStore } from 'pinia';
import { ref } from 'vue';
import { useNotificationsStore } from '@/stores/notifications';
import { postScheduleDistribution, type ScheduleDistributionRequest } from './calendarSchedulingDistributionApi';

export const useScheduleDistributionStore = defineStore('scheduleDistribution', () => {
  const isPending = ref(false);
  const notifications = useNotificationsStore();

  function send(request: ScheduleDistributionRequest) {
    // One distribution at a time, including when the user reopens the modal or navigates away.
    if (isPending.value) return false;
    isPending.value = true;
    notifications.show('Sending distribution…', 'info', true);
    void complete(request);
    return true;
  }

  async function complete(request: ScheduleDistributionRequest) {
    try {
      const { data, error, execute } = postScheduleDistribution(request);
      await execute();
      if (error.value || typeof data.value?.recipientCount !== 'number') {
        notifications.show('Distribution failed. Please try again.', 'error');
        return;
      }
      notifications.show('Distribution submitted successfully.', 'success');
    } catch {
      notifications.show('Distribution failed. Please try again.', 'error');
    } finally {
      isPending.value = false;
    }
  }

  return { isPending, send };
});
