<script setup lang="ts">
import { computed, ref } from 'vue';
import { Permissions, type LoanOutEntryResponse } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import { deleteLoanOutEntry, deleteLoanOutSeries } from '@/modules/loanout/api/loanOutApi';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';

const props = defineProps<{
  entry: LoanOutEntryResponse;
  scope?: 'event' | 'series';
  loanOutLocationName: string;
  dateLabel: string;
  otherEmployeeNames?: string;
}>();
const emit = defineEmits<{
  (event: 'close'): void;
  (event: 'deleted'): void;
}>();
const accessControl = useAccessControl();
const canDelete = computed(
  () =>
    Boolean(accessControl.featureFlags.value?.LoanOut?.enabled) &&
    accessControl.hasPermission(Permissions.UsersLoanOutDelete),
);
const isDeleting = ref(false);
const apiError = ref('');
const targetId = computed(() => (props.scope === 'series' ? props.entry.loanOutSeriesId : props.entry.id));
async function handleDelete() {
  if (!canDelete.value || isDeleting.value) return;
  if (targetId.value == null) {
    apiError.value =
      props.scope === 'series'
        ? 'Cannot delete this loan-out series because its ID is missing.'
        : 'Cannot delete this loan-out because its ID is missing.';
    return;
  }
  isDeleting.value = true;
  apiError.value = '';
  try {
    const { error } = await (props.scope === 'series'
      ? deleteLoanOutSeries(targetId.value)
      : deleteLoanOutEntry(targetId.value));
    if (error.value) {
      apiError.value = error.value.message || 'Failed to delete loan-out.';
      return;
    }
    emit('deleted');
    emit('close');
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'Failed to delete loan-out.';
  } finally {
    isDeleting.value = false;
  }
}
</script>

<template>
  <UaModal
    :title="scope === 'series' ? 'Delete Loan-out Series' : 'Delete Loan-out'"
    tone="error"
    :loading="isDeleting"
    @close="emit('close')"
  >
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">{{ apiError }}</UaAlert>
    </template>
    <p v-if="scope === 'series'">
      Are you sure you want to delete the entire <strong>{{ loanOutLocationName }}</strong> loan-out series? This
      permanently removes all draft occurrences and cannot be undone.
    </p>
    <p v-else>
      Are you sure you want to delete the loan-out to <strong>{{ loanOutLocationName }}</strong> on
      <strong>{{ dateLabel }}</strong
      >?
    </p>
    <p v-if="otherEmployeeNames">
      This will also remove {{ scope === 'series' ? 'the series' : 'this occurrence' }} for
      <strong>{{ otherEmployeeNames }}</strong
      >.
    </p>
    <template #actions>
      <UaBtn variant="outlined" :disabled="isDeleting" @click="emit('close')">Cancel</UaBtn>
      <UaBtn
        color="error"
        :disabled="!canDelete || isDeleting || targetId == null"
        :loading="isDeleting"
        @click="handleDelete"
        >Delete</UaBtn
      >
    </template>
  </UaModal>
</template>
