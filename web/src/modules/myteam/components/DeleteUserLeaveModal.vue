<script setup lang="ts">
import type { SchedulingLeaveEntryResponse as LeaveEntryResponse } from '@/api-access/generated/models';
import { deleteLeaveEntry, deleteLeaveSeries } from '@/modules/leave/api/leaveApi';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { ref } from 'vue';

const props = defineProps<{
  entry: LeaveEntryResponse;
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
const targetId = () => (props.scope === 'series' ? props.entry.leaveSeriesId : props.entry.id);

const handleDelete = async () => {
  const id = targetId();
  if (id == null) {
    apiError.value =
      props.scope === 'series'
        ? 'Cannot delete this leave series because its ID is missing.'
        : 'Cannot delete this leave because its ID is missing.';
    return;
  }

  isDeleting.value = true;
  apiError.value = '';

  try {
    const { error } = await (props.scope === 'series' ? deleteLeaveSeries(id) : deleteLeaveEntry(id));
    if (error.value) {
      apiError.value = error.value.message || 'Failed to delete leave.';
      return;
    }

    emit('deleted');
    emit('close');
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'Failed to delete leave.';
  } finally {
    isDeleting.value = false;
  }
};
</script>

<template>
  <UaModal
    :title="scope === 'series' ? 'Delete Leave Series' : 'Delete Leave'"
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
      Deleting this leave series will remove leave for
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
      <UaBtn color="error" :disabled="isDeleting || targetId() == null" :loading="isDeleting" @click="handleDelete">
        Delete
      </UaBtn>
    </template>
  </UaModal>
</template>
