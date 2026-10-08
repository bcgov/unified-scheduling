<script setup lang="ts">
import type { SchedulingTimeOffEntryResponse as TimeOffEntryResponse } from '@/api-access/generated/models';
import { deleteTimeOffEntry, deleteTimeOffSeries } from '@/modules/timeoff/api/timeOffApi';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { ref } from 'vue';

const props = defineProps<{
  entry: TimeOffEntryResponse;
  scope?: 'event' | 'series';
  leaveTypeName: string;
  dateLabel: string;
  otherEmployeeNames?: string;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'deleted'): void;
}>();

const isDeleting = ref(false);
const apiError = ref('');

const handleDelete = async () => {
  const id = props.scope === 'series' ? props.entry.timeOffSeriesId : props.entry.id;
  if (id == null) {
    return;
  }

  isDeleting.value = true;
  apiError.value = '';

  try {
    const { error } = await (props.scope === 'series' ? deleteTimeOffSeries(id) : deleteTimeOffEntry(id));
    if (error.value) {
      apiError.value = error.value.message || 'Failed to delete time off.';
      return;
    }

    emit('deleted');
    emit('close');
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'Failed to delete time off.';
  } finally {
    isDeleting.value = false;
  }
};
</script>

<template>
  <UaModal
    :title="scope === 'series' ? 'Delete Time Off Series' : 'Delete Leave/Time off'"
    tone="error"
    :loading="isDeleting"
    @close="emit('close')"
  >
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
    </template>

    <p v-if="scope === 'series' && otherEmployeeNames">
      Deleting this time-off series will remove time-off for
      <strong>{{ otherEmployeeNames }}</strong>
      in addition to you. This permanently removes all draft occurrences and cannot be undone. Do you want to continue?
    </p>
    <p v-else-if="scope === 'series'">
      Are you sure you want to delete the entire <strong>{{ leaveTypeName }}</strong> series? Deleting the series
      permanently removes all draft occurrences and cannot be undone.
    </p>
    <p v-else>
      Are you sure you want to delete
      <strong>{{ leaveTypeName }}</strong>
      on
      <strong>{{ dateLabel }}</strong>
      ?
    </p>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isDeleting" @click="emit('close')">Cancel</UaBtn>
      <UaBtn color="error" :loading="isDeleting" @click="handleDelete">Delete</UaBtn>
    </template>
  </UaModal>
</template>
