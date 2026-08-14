<script setup lang="ts">
import { patchApiLookupTrainingsIdOrder } from '@/api-access/generated/training/training';
import { Permissions } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import { mdiPlus } from '@mdi/js';
import { computed, ref, watch } from 'vue';
import type { TrainingLookupResponse } from '@/api-access/generated/models';
import TrainingCreateModal from './components/TrainingCreateModal.vue';
import TrainingEditModal from './components/TrainingEditModal.vue';
import TrainingExpireModal from './components/TrainingExpireModal.vue';
import TrainingTable from './components/TrainingTable.vue';
import { expireTrainingLookup, unexpireTrainingLookup, useTrainingLookup } from './trainingLookupApi';

const accessControl = useAccessControl();

const canViewTrainings = computed(() => accessControl.hasPermission(Permissions.TrainingsView));
const canCreateTrainings = computed(() => accessControl.hasPermission(Permissions.TrainingsCreate));
const canEditTrainings = computed(() => accessControl.hasPermission(Permissions.TrainingsEdit));

const trainingVisibilityFilter = ref<'active' | 'all'>('active');
const includeExpiredTrainings = computed(() => trainingVisibilityFilter.value === 'all');

const {
  data: trainings,
  error,
  isFetching,
  execute,
} = useTrainingLookup(includeExpiredTrainings, {
  options: {
    immediate: false,
  },
});

watch(
  canViewTrainings,
  (canView) => {
    if (canView) {
      void execute();
    }
  },
  { immediate: true },
);

const trainingRows = computed(() => trainings.value ?? []);
const showCreateTrainingModal = ref(false);
const selectedTraining = ref<TrainingLookupResponse | null>(null);
const selectedTrainingForExpire = ref<TrainingLookupResponse | null>(null);
const expireActionMode = ref<'expire' | 'unexpire'>('expire');
const isReordering = ref(false);
const isExpiring = ref(false);

const isTableLoading = computed(() => isFetching.value || isReordering.value || isExpiring.value);
const isReorderDisabled = computed(() => trainingVisibilityFilter.value === 'all');

const handleOpenCreateTraining = () => {
  showCreateTrainingModal.value = true;
};

const handleCreateModalClose = () => {
  showCreateTrainingModal.value = false;
};

const handleTrainingCreated = async () => {
  await execute();
  showCreateTrainingModal.value = false;
};

const handleEditTraining = (training: TrainingLookupResponse) => {
  selectedTraining.value = training;
};

const handleEditModalClose = () => {
  selectedTraining.value = null;
};

const handleTrainingUpdated = async () => {
  await execute();
  selectedTraining.value = null;
};

const handleTrainingReorder = async ({ trainingId, newOrder }: { trainingId: number; newOrder: number }) => {
  if (isReordering.value) {
    return;
  }

  isReordering.value = true;

  try {
    const { error } = await patchApiLookupTrainingsIdOrder(trainingId, { newOrder });
    if (error.value) {
      console.error('Failed to reorder trainings:', error.value.message);
    }

    await execute();
  } finally {
    isReordering.value = false;
  }
};

const handleExpireTraining = (training: TrainingLookupResponse) => {
  expireActionMode.value = 'expire';
  selectedTrainingForExpire.value = training;
};

const handleUnexpireTraining = async (training: TrainingLookupResponse) => {
  expireActionMode.value = 'unexpire';
  selectedTrainingForExpire.value = training;
};

const handleExpireModalClose = () => {
  selectedTrainingForExpire.value = null;
};

const handleConfirmExpireTraining = async () => {
  const training = selectedTrainingForExpire.value;
  if (!training || isExpiring.value) {
    return;
  }

  isExpiring.value = true;

  try {
    const { error } =
      expireActionMode.value === 'unexpire'
        ? await unexpireTrainingLookup(training.id)
        : await expireTrainingLookup(training.id);

    if (error.value) {
      console.error(
        `Failed to ${expireActionMode.value === 'unexpire' ? 'unexpire' : 'expire'} training:`,
        error.value.message,
      );
    }

    await execute();
    selectedTrainingForExpire.value = null;
  } finally {
    isExpiring.value = false;
  }
};

watch(includeExpiredTrainings, () => {
  if (canViewTrainings.value) {
    void execute();
  }
});
</script>

<template>
  <div v-if="!canViewTrainings" class="training-page">
    <UaPlaceholderPage title="Training" description="You do not have permission to view trainings." />
  </div>

  <div v-else class="training-page">
    <div class="training-header">
      <div>
        <h2 class="page-title">Training</h2>
      </div>

      <div class="training-header__actions">
        <v-switch
          v-model="trainingVisibilityFilter"
          class="training-header__filter"
          color="primary"
          base-color="primary"
          hide-details
          inset
          aria-label="Show all trainings including expired"
          :label="includeExpiredTrainings ? 'All Trainings' : 'Active Trainings'"
          false-value="active"
          true-value="all"
        />

        <UaBtn v-if="canCreateTrainings" :prepend-icon="mdiPlus" @click="handleOpenCreateTraining">Add Training</UaBtn>
      </div>
    </div>

    <UaAlert v-if="error" type="error">Failed to load trainings: {{ error.message }}</UaAlert>

    <TrainingTable
      :items="trainingRows"
      :loading="isTableLoading"
      :can-edit="canEditTrainings"
      :disable-reorder="isReorderDisabled"
      :highlight-expired-rows="includeExpiredTrainings"
      @edit="handleEditTraining"
      @expire="handleExpireTraining"
      @unexpire="handleUnexpireTraining"
      @reorder="handleTrainingReorder"
    />

    <TrainingCreateModal
      v-if="showCreateTrainingModal"
      @close="handleCreateModalClose"
      @created="handleTrainingCreated"
    />

    <TrainingEditModal
      v-if="selectedTraining"
      :training="selectedTraining"
      @close="handleEditModalClose"
      @updated="handleTrainingUpdated"
    />

    <TrainingExpireModal
      v-if="selectedTrainingForExpire"
      :training="selectedTrainingForExpire"
      :mode="expireActionMode"
      :loading="isExpiring"
      @close="handleExpireModalClose"
      @confirm="handleConfirmExpireTraining"
    />
  </div>
</template>

<style scoped>
.training-page {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-lg);
  padding: var(--ua-spacing-xl);
}

.training-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--ua-spacing-md);
}

.training-header__actions {
  display: flex;
  align-items: center;
  gap: var(--ua-spacing-md);
  margin-left: auto;
}

.training-header__filter {
  min-width: 0;
  width: auto;
  flex: 0 0 auto;
  margin: 0;
}

.training-header__filter :deep(.v-input__control) {
  width: auto;
}

.training-header__filter :deep(.v-selection-control) {
  min-height: 32px;
}

.training-header__filter :deep(.v-label) {
  font-weight: var(--ua-font-weight-medium);
}

.training-header__filter :deep(.v-switch__track) {
  opacity: 1;
  border: 1px solid rgba(var(--v-theme-primary), 0.45);
  background-color: rgba(var(--v-theme-primary), 0.2);
}

.training-header__filter :deep(.v-selection-control--dirty .v-switch__track) {
  background-color: rgba(var(--v-theme-primary), 0.45);
}

.training-header__filter :deep(.v-switch__thumb) {
  box-shadow: 0 0 0 1px rgba(var(--v-theme-primary), 0.5);
}

.page-title {
  margin: 0;
  font-size: var(--ua-font-size-xl);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
}

.page-subtitle {
  margin: var(--ua-spacing-xs) 0 0;
  color: var(--ua-text-secondary);
}

@media (max-width: 768px) {
  .training-page {
    padding: var(--ua-spacing-lg);
  }

  .training-header {
    flex-direction: column;
  }

  .training-header__actions {
    width: 100%;
    flex-direction: column;
    align-items: stretch;
  }
}
</style>
