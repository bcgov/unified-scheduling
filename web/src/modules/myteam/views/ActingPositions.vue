<script setup lang="ts">
import { getApiUsersUserIdActingPositions } from '@/api-access/generated/acting-positions/acting-positions';
import {
  LookupCodeTypes,
  Permissions,
  type UserResponse,
  type ActingPositionResponseDto,
} from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import { useLookupStore } from '@/stores/LookupStore';
import { toCalendarDateString } from '@/utils/date';
import { mdiClockRemove, mdiPencil, mdiPlus } from '@mdi/js';
import { computed, onMounted, ref } from 'vue';
import ActingPositionModal from '../components/ActingPositionModal.vue';
import ExpireActingPositionModal from '../components/ExpireActingPositionModal.vue';

const props = defineProps<{
  user: UserResponse;
}>();

const userId = computed(() => props.user.id);

const accessControl = useAccessControl();
const lookupStore = useLookupStore();

onMounted(() => lookupStore.load(LookupCodeTypes.PositionTypes));

const {
  data: actingPositions,
  error: actingPositionsError,
  isFetching: isFetchingPositions,
  execute: fetchActingPositions,
} = getApiUsersUserIdActingPositions(props.user.id);

const showActingPositionModal = ref(false);
const selectedPosition = ref<ActingPositionResponseDto | null>(null);
const showExpireModal = ref(false);
const selectedExpirePosition = ref<ActingPositionResponseDto | null>(null);

const tableHeaders = [
  { title: 'Position Type', key: 'positionTypeDescription', sortable: true },
  { title: 'Effective Date', key: 'effectiveDate', sortable: true },
  { title: 'Expiry Date', key: 'expiryDate', sortable: false },
  { title: 'Comment', key: 'comment', sortable: false },
  { title: 'Actions', key: 'actions', sortable: false, align: 'end' as const, width: 140 },
];

const isExpired = (position: ActingPositionResponseDto): boolean => {
  if (!position.expiryDate) return false;
  return new Date(position.expiryDate) < new Date();
};

const handleOpenAddModal = () => {
  selectedPosition.value = null;
  showActingPositionModal.value = true;
};

const handleOpenEditModal = (position: ActingPositionResponseDto) => {
  selectedPosition.value = position;
  showActingPositionModal.value = true;
};

const handleModalClose = () => {
  showActingPositionModal.value = false;
  selectedPosition.value = null;
};

const handleOpenExpireModal = (position: ActingPositionResponseDto) => {
  selectedExpirePosition.value = position;
  showExpireModal.value = true;
};

const handleExpireModalClose = () => {
  showExpireModal.value = false;
  selectedExpirePosition.value = null;
};

const handleSaved = async () => {
  await fetchActingPositions();
};

const handleExpired = async () => {
  await fetchActingPositions();
};
</script>

<template>
  <div class="acting-positions">
    <div class="acting-positions-header">
      <h3>Acting Positions</h3>
      <UaBtn
        v-if="accessControl.hasPermission(Permissions.ActingPositionsCreate)"
        :prepend-icon="mdiPlus"
        @click="handleOpenAddModal"
      >
        Add Acting Position
      </UaBtn>
    </div>

    <UaAlert v-if="actingPositionsError" type="error" :closable="false">
      Failed to load acting positions: {{ actingPositionsError.message }}
    </UaAlert>

    <div v-if="isFetchingPositions" class="loading-state">Loading acting positions...</div>

    <UaDataTable
      v-else-if="actingPositions && actingPositions.length"
      :headers="tableHeaders"
      :items="actingPositions"
      :items-per-page="-1"
      density="comfortable"
      hide-default-footer
    >
      <template #[`item.effectiveDate`]="{ item }">
        {{ toCalendarDateString(item.effectiveDate) ?? '-' }}
      </template>

      <template #[`item.expiryDate`]="{ item }">
        {{ toCalendarDateString(item.expiryDate) ?? '-' }}
      </template>

      <template #[`item.comment`]="{ item }">
        {{ item.comment ?? '-' }}
      </template>

      <template #[`item.actions`]="{ item }">
        <div class="col-actions">
          <UaBtn
            v-if="accessControl.hasPermission(Permissions.ActingPositionsEdit) && !isExpired(item)"
            icon
            variant="text"
            size="small"
            aria-label="Edit acting position"
            title="Edit acting position"
            @click="handleOpenEditModal(item)"
          >
            <v-icon :icon="mdiPencil" />
          </UaBtn>
          <UaBtn
            v-if="accessControl.hasPermission(Permissions.ActingPositionsExpire) && !isExpired(item)"
            icon
            variant="text"
            size="small"
            color="error"
            aria-label="Expire acting position"
            title="Expire acting position"
            @click="handleOpenExpireModal(item)"
          >
            <v-icon :icon="mdiClockRemove" />
          </UaBtn>
        </div>
      </template>
    </UaDataTable>

    <UaPlaceholderPage
      v-else-if="!isFetchingPositions && !actingPositionsError"
      title="No acting positions"
      description="No acting positions have been assigned."
    />

    <ActingPositionModal
      v-if="showActingPositionModal"
      :user-id="userId"
      :position-types="lookupStore.getSelectOptions(LookupCodeTypes.PositionTypes)"
      :position="selectedPosition"
      @close="handleModalClose"
      @saved="handleSaved"
    />

    <ExpireActingPositionModal
      v-if="showExpireModal && selectedExpirePosition"
      :user-id="userId"
      :position="selectedExpirePosition"
      @close="handleExpireModalClose"
      @expired="handleExpired"
    />
  </div>
</template>

<style scoped>
.acting-positions {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.acting-positions-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.col-actions {
  display: flex;
  gap: 4px;
  justify-content: flex-end;
}
</style>
