<script setup lang="ts">
import { getApiLookupTrainings, patchApiLookupTrainingsIdOrder } from '@/api-access/generated/training/training';
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
import TrainingTable from './components/TrainingTable.vue';

const accessControl = useAccessControl();

const canViewTrainings = computed(() => accessControl.hasPermission(Permissions.TrainingsView));
const canCreateTrainings = computed(() => accessControl.hasPermission(Permissions.TrainingsCreate));
const canEditTrainings = computed(() => accessControl.hasPermission(Permissions.TrainingsEdit));

const {
  data: trainings,
  error,
  isFetching,
  execute,
} = getApiLookupTrainings({
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
const isReordering = ref(false);

const isTableLoading = computed(() => isFetching.value || isReordering.value);

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

      <UaBtn v-if="canCreateTrainings" :prepend-icon="mdiPlus" @click="handleOpenCreateTraining">Add Training</UaBtn>
    </div>

    <UaAlert v-if="error" type="error">Failed to load trainings: {{ error.message }}</UaAlert>

    <TrainingTable
      :items="trainingRows"
      :loading="isTableLoading"
      :can-edit="canEditTrainings"
      @edit="handleEditTraining"
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
}
</style>
