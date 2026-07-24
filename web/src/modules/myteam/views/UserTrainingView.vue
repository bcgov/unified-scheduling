<script setup lang="ts">
import { getApiLookupTrainings } from '@/api-access/generated/training/training';
import type { TrainingLookupResponse } from '@/api-access/generated/models';
import { getApiTrainingUserTrainings } from '@/api-access/generated/user-training/user-training';
import type { UserTrainingResponse } from '@/api-access/generated/models';
import type { UserResponse } from '@/api-access/generated/models';
import type { Permissions } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import { toCalendarDateString } from '@/utils/date';
import type { SelectOption } from '@/types/select';
import { mdiDelete, mdiPencil, mdiPlus } from '@mdi/js';
import { computed, ref } from 'vue';
import DeleteUserTrainingModal from '../components/DeleteUserTrainingModal.vue';
import UserTrainingModal from '../components/UserTrainingModal.vue';

const props = defineProps<{
  user: UserResponse;
}>();

const accessControl = useAccessControl();
const userTrainingsViewPermission = 'UserTrainingsView' as Permissions;
const userTrainingsCreatePermission = 'UserTrainingsCreate' as Permissions;
const userTrainingsEditPermission = 'UserTrainingsEdit' as Permissions;
const userTrainingsDeletePermission = 'UserTrainingsDelete' as Permissions;

const canViewTrainings = computed(() => accessControl.hasPermission(userTrainingsViewPermission));
const canCreateTrainings = computed(() => accessControl.hasPermission(userTrainingsCreatePermission));
const canEditTrainings = computed(() => accessControl.hasPermission(userTrainingsEditPermission));
const canDeleteTrainings = computed(() => accessControl.hasPermission(userTrainingsDeletePermission));

const userTrainingParams = computed(() => ({
  userId: props.user.id,
}));

const {
  data: userTrainings,
  error: userTrainingsError,
  isFetching: isFetchingUserTrainings,
  execute: fetchUserTrainings,
} = getApiTrainingUserTrainings(userTrainingParams, {
  options: {
    immediate: false,
  },
});

const {
  data: trainings,
  error: trainingsError,
  isFetching: isFetchingTrainings,
  execute: fetchTrainings,
} = getApiLookupTrainings({
  options: {
    immediate: false,
  },
});

if (canViewTrainings.value) {
  void fetchUserTrainings();
  void fetchTrainings();
}

const showTrainingModal = ref(false);
const selectedTraining = ref<UserTrainingResponse | null>(null);
const showDeleteModal = ref(false);
const selectedDeleteTraining = ref<UserTrainingResponse | null>(null);

const headers = computed(() => [
  { title: 'Training', key: 'trainingCode', sortable: true },
  { title: 'Category', key: 'trainingCategoryName', sortable: true },
  { title: 'Awarded On', key: 'awardedOn', sortable: true },
  { title: 'Expiry Date', key: 'expiryDate', sortable: true },
  { title: 'Status', key: 'status', sortable: false },
  { title: 'Notice State', key: 'noticeState', sortable: true },
  { title: 'Notes', key: 'notes', sortable: false },
  { title: 'Actions', key: 'actions', sortable: false, align: 'end' as const, width: 140 },
]);

const trainingOptions = computed<SelectOption[]>(() =>
  (trainings.value ?? []).map((training: TrainingLookupResponse) => ({
    code: training.id,
    description:
      training.code && training.description
        ? training.code === training.description
          ? training.code
          : `${training.code} - ${training.description}`
        : (training.code ?? training.description ?? ''),
  })),
);

const handleOpenAddModal = () => {
  selectedTraining.value = null;
  showTrainingModal.value = true;
};

const handleOpenEditModal = (training: UserTrainingResponse) => {
  selectedTraining.value = training;
  showTrainingModal.value = true;
};

const handleCloseTrainingModal = () => {
  showTrainingModal.value = false;
  selectedTraining.value = null;
};

const handleSaved = async () => {
  await fetchUserTrainings();
};

const handleOpenDeleteModal = (training: UserTrainingResponse) => {
  selectedDeleteTraining.value = training;
  showDeleteModal.value = true;
};

const handleCloseDeleteModal = () => {
  showDeleteModal.value = false;
  selectedDeleteTraining.value = null;
};

const handleDeleted = async () => {
  await fetchUserTrainings();
};

const getTrainingStatus = (training: UserTrainingResponse) => {
  if (!training.expiryDate) {
    return 'Active';
  }

  return new Date(training.expiryDate).getTime() > Date.now() ? 'Active' : 'Historical';
};

const combinedError = computed(() => userTrainingsError.value ?? trainingsError.value);
</script>

<template>
  <div v-if="!canViewTrainings" class="user-training-view">
    <UaPlaceholderPage title="Training" description="You do not have permission to view training records." />
  </div>

  <div v-else class="user-training-view">
    <div class="user-training-view__header">
      <h3>Training</h3>
      <UaBtn v-if="canCreateTrainings" :prepend-icon="mdiPlus" @click="handleOpenAddModal">Add Training</UaBtn>
    </div>

    <UaAlert v-if="combinedError" type="error" :closable="false">
      Failed to load training records: {{ combinedError.message }}
    </UaAlert>

    <div v-if="isFetchingUserTrainings || isFetchingTrainings" class="user-training-view__loading">
      Loading training records...
    </div>

    <UaDataTable
      v-else-if="userTrainings && userTrainings.length"
      :headers="headers"
      :items="userTrainings"
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

      <template #[`item.actions`]="{ item }">
        <div class="user-training-view__actions">
          <UaBtn
            v-if="canEditTrainings"
            icon
            variant="text"
            size="small"
            aria-label="Edit training record"
            title="Edit training record"
            @click="handleOpenEditModal(item)"
          >
            <v-icon :icon="mdiPencil" />
          </UaBtn>
          <UaBtn
            v-if="canDeleteTrainings"
            icon
            variant="text"
            size="small"
            color="error"
            aria-label="Delete training record"
            title="Delete training record"
            @click="handleOpenDeleteModal(item)"
          >
            <v-icon :icon="mdiDelete" />
          </UaBtn>
        </div>
      </template>
    </UaDataTable>

    <UaPlaceholderPage
      v-else-if="!isFetchingUserTrainings && !combinedError"
      title="No training records"
      description="No training records have been recorded for this user yet."
    />

    <UserTrainingModal
      v-if="showTrainingModal"
      :user-id="props.user.id"
      :training-options="trainingOptions"
      :training="selectedTraining"
      @close="handleCloseTrainingModal"
      @saved="handleSaved"
    />

    <DeleteUserTrainingModal
      v-if="showDeleteModal && selectedDeleteTraining"
      :training="selectedDeleteTraining"
      @close="handleCloseDeleteModal"
      @deleted="handleDeleted"
    />
  </div>
</template>

<style scoped>
.user-training-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.user-training-view__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.user-training-view__loading {
  color: var(--ua-text-secondary);
}

.user-training-view__actions {
  display: flex;
  gap: 4px;
  justify-content: flex-end;
}
</style>
