<script setup lang="ts">
import { getApiLookupTrainings } from '@/api-access/generated/training/training';
import type { TrainingLookupResponse } from '@/api-access/generated/models';
import { getApiTrainingsUsersUserId } from '@/api-access/generated/user-training/user-training';
import type { UserTrainingResponse } from '@/api-access/generated/models';
import type { UserResponse } from '@/api-access/generated/models';
import { Permissions } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import { toCalendarDateString } from '@/utils/date';
import type { SelectOption } from '@/types/select';
import { mdiAutorenew, mdiDelete, mdiPencil, mdiPlus } from '@mdi/js';
import { computed, ref } from 'vue';
import DeleteUserTrainingModal from '../components/DeleteUserTrainingModal.vue';
import UserTrainingModal from '../components/UserTrainingModal.vue';
import UserTrainingVersionsModal from '../components/UserTrainingVersionsModal.vue';

const props = defineProps<{
  user: UserResponse;
}>();

const accessControl = useAccessControl();
const canViewTrainings = computed(() => accessControl.hasPermission(Permissions.UserTrainingsView));
const canCreateTrainings = computed(() => accessControl.hasPermission(Permissions.UserTrainingsCreate));
const canEditTrainings = computed(() => accessControl.hasPermission(Permissions.UserTrainingsEdit));
const canDeleteTrainings = computed(() => accessControl.hasPermission(Permissions.UserTrainingsDelete));

const {
  data: userTrainings,
  error: userTrainingsError,
  isFetching: isFetchingUserTrainings,
  execute: fetchUserTrainings,
} = getApiTrainingsUsersUserId(props.user.id, {
  options: {
    immediate: false,
  },
});

const {
  data: trainings,
  error: trainingsError,
  isFetching: isFetchingTrainings,
  execute: fetchTrainings,
} = getApiLookupTrainings(undefined, {
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
const trainingModalMode = ref<'create' | 'edit' | 'renew'>('create');
const showDeleteModal = ref(false);
const selectedDeleteTraining = ref<UserTrainingResponse | null>(null);
const showDetailsModal = ref(false);
const selectedDetailsTraining = ref<UserTrainingResponse | null>(null);

const headers = computed(() => [
  { title: 'Training', key: 'trainingCode', sortable: true },
  { title: 'Category', key: 'trainingCategoryName', sortable: true },
  { title: 'From', key: 'awardedOn', sortable: true },
  { title: 'To', key: 'endingOn', sortable: true },
  { title: 'Expiry Date', key: 'expiryDate', sortable: true },
  { title: 'Status', key: 'status', sortable: false },
  { title: 'Notice State', key: 'noticeState', sortable: true },
  { title: 'Notes', key: 'notes', sortable: false },
  { title: 'Actions', key: 'actions', sortable: false, align: 'end' as const, width: 190 },
]);

const trainingOptions = computed<SelectOption[]>(() =>
  (trainings.value ?? []).map((training: TrainingLookupResponse) => ({
    code: training.id,
    description: training.description?.trim() || training.code?.trim() || `Training ${training.id}`,
  })),
);

const assignedTrainingIds = computed(() => new Set((userTrainings.value ?? []).map((training) => training.trainingId)));

const rotatingTrainingIds = computed(
  () =>
    new Set(
      (trainings.value ?? [])
        .filter((training: TrainingLookupResponse) => training.rotating)
        .map((training: TrainingLookupResponse) => training.id),
    ),
);

const compareExpiryDesc = (left: UserTrainingResponse, right: UserTrainingResponse) => {
  const leftExpiry = left.expiryDate ? new Date(left.expiryDate).getTime() : Number.POSITIVE_INFINITY;
  const rightExpiry = right.expiryDate ? new Date(right.expiryDate).getTime() : Number.POSITIVE_INFINITY;

  if (leftExpiry !== rightExpiry) {
    return rightExpiry - leftExpiry;
  }

  const leftVersion = (left as UserTrainingResponse & { version?: number }).version ?? 0;
  const rightVersion = (right as UserTrainingResponse & { version?: number }).version ?? 0;
  if (leftVersion !== rightVersion) {
    return rightVersion - leftVersion;
  }

  return right.id - left.id;
};

const latestUserTrainings = computed<UserTrainingResponse[]>(() => {
  const groups = new Map<number, UserTrainingResponse[]>();

  for (const training of userTrainings.value ?? []) {
    const existing = groups.get(training.trainingId) ?? [];
    existing.push(training);
    groups.set(training.trainingId, existing);
  }

  return Array.from(groups.values())
    .map((group) => [...group].sort(compareExpiryDesc)[0])
    .filter((training): training is UserTrainingResponse => !!training)
    .sort((left, right) => {
      const leftCode = left.trainingCode ?? '';
      const rightCode = right.trainingCode ?? '';
      if (leftCode !== rightCode) {
        return leftCode.localeCompare(rightCode);
      }

      return compareExpiryDesc(left, right);
    });
});

const selectedTrainingVersions = computed<UserTrainingResponse[]>(() => {
  if (!selectedDetailsTraining.value) {
    return [];
  }

  return (userTrainings.value ?? [])
    .filter((training) => training.trainingId === selectedDetailsTraining.value?.trainingId)
    .sort(compareExpiryDesc);
});

const availableTrainingOptions = computed<SelectOption[]>(() =>
  trainingOptions.value.filter((option) => !assignedTrainingIds.value.has(Number(option.code))),
);

const handleOpenAddModal = () => {
  trainingModalMode.value = 'create';
  selectedTraining.value = null;
  showTrainingModal.value = true;
};

const handleOpenEditModal = (training: UserTrainingResponse) => {
  trainingModalMode.value = 'edit';
  selectedTraining.value = training;
  showTrainingModal.value = true;
};

const handleOpenRenewModal = (training: UserTrainingResponse) => {
  trainingModalMode.value = 'renew';
  selectedTraining.value = training;
  showTrainingModal.value = true;
};

const handleCloseTrainingModal = () => {
  trainingModalMode.value = 'create';
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

const handleOpenDetailsModal = (training: UserTrainingResponse) => {
  selectedDetailsTraining.value = training;
  showDetailsModal.value = true;
};

const handleCloseDetailsModal = () => {
  selectedDetailsTraining.value = null;
  showDetailsModal.value = false;
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

  return new Date(training.expiryDate).getTime() > Date.now() ? 'Active' : 'Expired';
};

const canRenewTraining = (training: UserTrainingResponse) => rotatingTrainingIds.value.has(training.trainingId);

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
      v-else-if="latestUserTrainings.length"
      :headers="headers"
      :items="latestUserTrainings"
      :items-per-page="-1"
      density="comfortable"
      hide-default-footer
    >
      <template #[`item.trainingCode`]="{ item }">
        <UaBtn
          variant="text"
          class="user-training-view__training-link"
          :title="`View ${item.trainingCode} details`"
          @click="handleOpenDetailsModal(item)"
        >
          <span class="user-training-view__training-link-text">{{ item.trainingCode }}</span>
        </UaBtn>
      </template>

      <template #[`item.awardedOn`]="{ item }">
        {{ toCalendarDateString(item.awardedOn) ?? '-' }}
      </template>

      <template #[`item.endingOn`]="{ item }">
        {{ toCalendarDateString(item.endingOn ?? item.awardedOn) ?? '-' }}
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
            v-if="canCreateTrainings && canRenewTraining(item)"
            icon
            variant="text"
            size="small"
            aria-label="Renew training record"
            title="Renew training record"
            @click="handleOpenRenewModal(item)"
          >
            <v-icon :icon="mdiAutorenew" />
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
      :mode="trainingModalMode"
      :training-options="availableTrainingOptions"
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

    <UserTrainingVersionsModal
      v-if="showDetailsModal && selectedDetailsTraining"
      :training="selectedDetailsTraining"
      :trainings="selectedTrainingVersions"
      @close="handleCloseDetailsModal"
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

.user-training-view__training-link {
  justify-content: flex-start;
  padding-inline: 0;
  min-width: 0;
  text-transform: none;
  color: rgb(var(--v-theme-primary));
}

.user-training-view__training-link-text {
  text-decoration: underline;
  text-underline-offset: 2px;
}

.user-training-view__training-link:hover .user-training-view__training-link-text {
  opacity: 0.85;
}
</style>
