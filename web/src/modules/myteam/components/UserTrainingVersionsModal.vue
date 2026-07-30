<script setup lang="ts">
import type { UserTrainingResponse } from '@/api-access/generated/models';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { toCalendarDateString } from '@/utils/date';
import { computed, ref } from 'vue';

const props = defineProps<{
  training: UserTrainingResponse;
  trainings: UserTrainingResponse[];
}>();

const emit = defineEmits<{
  (e: 'close'): void;
}>();

const headers = computed(() => [
  { title: 'Awarded On', key: 'awardedOn', sortable: true },
  { title: 'Expiry Date', key: 'expiryDate', sortable: true },
  { title: 'Status', key: 'status', sortable: false },
  { title: 'Notice State', key: 'noticeState', sortable: true },
  { title: 'Notes', key: 'notes', sortable: false },
]);

const activeTab = ref<'details' | 'history'>('details');

const historyTrainings = computed(() => props.trainings.filter((training) => training.id !== props.training.id));

const getTrainingStatus = (training: UserTrainingResponse) => {
  if (!training.expiryDate) {
    return 'Active';
  }

  return new Date(training.expiryDate).getTime() > Date.now() ? 'Active' : 'Historical';
};
</script>

<template>
  <UaModal :title="`User Training: ${props.training.trainingCode}`" @close="emit('close')">
    <v-tabs v-model="activeTab" class="user-training-details__tabs" density="comfortable">
      <v-tab value="details">Details</v-tab>
      <v-tab value="history">History</v-tab>
    </v-tabs>

    <div v-if="activeTab === 'details'" class="user-training-details__panel" role="tabpanel">
      <v-row dense>
        <v-col cols="12" sm="4" class="user-training-details__label">Training</v-col>
        <v-col cols="12" sm="8">{{ props.training.trainingCode }}</v-col>

        <v-col cols="12" sm="4" class="user-training-details__label">Category</v-col>
        <v-col cols="12" sm="8">{{ props.training.trainingCategoryName }}</v-col>

        <v-col cols="12" sm="4" class="user-training-details__label">Awarded On</v-col>
        <v-col cols="12" sm="8">{{ toCalendarDateString(props.training.awardedOn) ?? '-' }}</v-col>

        <v-col cols="12" sm="4" class="user-training-details__label">Expiry Date</v-col>
        <v-col cols="12" sm="8">{{ toCalendarDateString(props.training.expiryDate) ?? 'Never' }}</v-col>

        <v-col cols="12" sm="4" class="user-training-details__label">Status</v-col>
        <v-col cols="12" sm="8">{{ getTrainingStatus(props.training) }}</v-col>

        <v-col cols="12" sm="4" class="user-training-details__label">Notice State</v-col>
        <v-col cols="12" sm="8">{{ props.training.noticeState }}</v-col>

        <v-col cols="12" sm="4" class="user-training-details__label">Notes</v-col>
        <v-col cols="12" sm="8">{{ props.training.notes?.trim() || '-' }}</v-col>
      </v-row>
    </div>

    <div v-else class="user-training-details__panel" role="tabpanel">
      <UaDataTable
        v-if="historyTrainings.length"
        :headers="headers"
        :items="historyTrainings"
        :items-per-page="-1"
        density="comfortable"
        hide-default-footer
      >
        <template #[`item.awardedOn`]="{ item }">
          {{ toCalendarDateString(item.awardedOn) ?? '-' }}
        </template>

        <template #[`item.expiryDate`]="{ item }">
          {{ toCalendarDateString(item.expiryDate) ?? 'Never' }}
        </template>

        <template #[`item.status`]="{ item }">
          {{ getTrainingStatus(item) }}
        </template>

        <template #[`item.notes`]="{ item }">
          {{ item.notes?.trim() || '-' }}
        </template>
      </UaDataTable>
      <p v-else class="user-training-details__empty">No historical versions found for this training.</p>
    </div>
  </UaModal>
</template>

<style scoped>
.user-training-details__tabs {
  margin-bottom: var(--ua-spacing-md);
}

.user-training-details__panel {
  min-height: 220px;
}

.user-training-details__rows {
  margin: 0;
}

.user-training-details__label {
  font-weight: var(--ua-font-weight-bold);
}

.user-training-details__empty {
  color: var(--ua-text-secondary);
  margin: 0;
}
</style>
