<script setup lang="ts">
import { computed, ref } from 'vue';
import { getApiTrainingsUsersUserId } from '@/api-access/generated/user-training/user-training';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UserTrainingVersionsModal from '@/modules/myteam/components/UserTrainingVersionsModal.vue';
import { isTrainingCalendarEvent } from '../contributions/calendarTrainingEventsContribution';

const props = defineProps<{ event: CalendarEventBase }>();
const emit = defineEmits<{ close: [] }>();

const trainingEvent = isTrainingCalendarEvent(props.event) ? props.event : undefined;
const { data, error, execute } = getApiTrainingsUsersUserId(trainingEvent?.userId ?? '', {
  options: { immediate: false },
});
const loading = ref(Boolean(trainingEvent));

if (trainingEvent) {
  void execute().finally(() => {
    loading.value = false;
  });
}

const selectedTraining = computed(() => data.value?.find((record) => record.id === trainingEvent?.userTrainingId));
const trainingVersions = computed(() =>
  (data.value ?? []).filter((record) => record.trainingId === selectedTraining.value?.trainingId),
);
</script>

<template>
  <UserTrainingVersionsModal
    v-if="selectedTraining"
    :training="selectedTraining"
    :trainings="trainingVersions"
    @close="emit('close')"
  />
  <UaModal v-else-if="!loading" :title="event.title" @close="emit('close')">
    <UaAlert v-if="error" type="error">Unable to load this training record.</UaAlert>
    <UaAlert v-else type="error">Training record not found.</UaAlert>
  </UaModal>
</template>
