<script setup lang="ts">
import { getApiUsersUserIdAwayLocations } from '@/api-access/generated/away-locations/away-locations';
import { Permissions, type AwayLocationResponseDto, type UserResponse } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import { toCalendarDateString, toTimeInputValue } from '@/utils/date';
import { mdiClockRemove, mdiPencil, mdiPlus } from '@mdi/js';
import { computed, onMounted, ref } from 'vue';
import AwayLocationModal from '../components/AwayLocationModal.vue';
import ExpireAwayLocationModal from '../components/ExpireAwayLocationModal.vue';

const props = defineProps<{
  user: UserResponse;
}>();

const userId = computed(() => props.user.id);

const accessControl = useAccessControl();
const locationsStore = useLocationsStore();

onMounted(() => locationsStore.getEntities());

const {
  data: awayLocations,
  error: awayLocationsError,
  isFetching: isFetchingAwayLocations,
  execute: fetchAwayLocations,
} = getApiUsersUserIdAwayLocations(props.user.id);

const showAwayLocationModal = ref(false);
const selectedAwayLocation = ref<AwayLocationResponseDto | null>(null);
const showExpireModal = ref(false);
const selectedExpireAwayLocation = ref<AwayLocationResponseDto | null>(null);

const tableHeaders = computed(() => [
  { title: 'Location', key: 'locationName', sortable: true },
  { title: 'Start Date', key: 'startAtUtc', sortable: true },
  { title: 'Start Time', key: 'startTime', sortable: false },
  { title: 'End Date', key: 'endAtUtc', sortable: false },
  { title: 'End Time', key: 'endTime', sortable: false },
  { title: 'Comment', key: 'comment', sortable: false },
  { title: 'Actions', key: 'actions', sortable: false, align: 'end' as const, width: 140 },
]);

const handleOpenAddModal = () => {
  selectedAwayLocation.value = null;
  showAwayLocationModal.value = true;
};

const handleOpenEditModal = (awayLocation: AwayLocationResponseDto) => {
  selectedAwayLocation.value = awayLocation;
  showAwayLocationModal.value = true;
};

const handleModalClose = () => {
  showAwayLocationModal.value = false;
  selectedAwayLocation.value = null;
};

const handleOpenExpireModal = (awayLocation: AwayLocationResponseDto) => {
  selectedExpireAwayLocation.value = awayLocation;
  showExpireModal.value = true;
};

const handleExpireModalClose = () => {
  showExpireModal.value = false;
  selectedExpireAwayLocation.value = null;
};

const handleSaved = async () => {
  await fetchAwayLocations();
};

const handleExpired = async () => {
  await fetchAwayLocations();
};
</script>

<template>
  <div class="away-locations">
    <div class="away-locations-header">
      <h3>Away Locations</h3>
      <UaBtn
        v-if="accessControl.hasPermission(Permissions.AwayLocationsCreate)"
        :prepend-icon="mdiPlus"
        @click="handleOpenAddModal"
      >
        Add Away Location
      </UaBtn>
    </div>

    <UaAlert v-if="awayLocationsError" type="error" :closable="false">
      Failed to load away locations: {{ awayLocationsError.message }}
    </UaAlert>

    <div v-if="isFetchingAwayLocations" class="loading-state">Loading away locations...</div>

    <UaDataTable
      v-else-if="awayLocations && awayLocations.length"
      :headers="tableHeaders"
      :items="awayLocations"
      :items-per-page="-1"
      density="comfortable"
      hide-default-footer
    >
      <template #[`item.startAtUtc`]="{ item }">
        {{ toCalendarDateString(item.startAtUtc) ?? '-' }}
      </template>

      <template #[`item.startTime`]="{ item }">
        {{ toTimeInputValue(item.startAtUtc) ?? '-' }}
      </template>

      <template #[`item.endAtUtc`]="{ item }">
        {{ toCalendarDateString(item.endAtUtc) ?? '-' }}
      </template>

      <template #[`item.endTime`]="{ item }">
        {{ toTimeInputValue(item.endAtUtc) ?? '-' }}
      </template>

      <template #[`item.comment`]="{ item }">
        {{ item.comment ?? '-' }}
      </template>

      <template #[`item.actions`]="{ item }">
        <div class="col-actions">
          <UaBtn
            v-if="accessControl.hasPermission(Permissions.AwayLocationsEdit)"
            icon
            variant="text"
            size="small"
            aria-label="Edit away location"
            title="Edit away location"
            @click="handleOpenEditModal(item)"
          >
            <v-icon :icon="mdiPencil" />
          </UaBtn>
          <UaBtn
            v-if="accessControl.hasPermission(Permissions.AwayLocationsExpire)"
            icon
            variant="text"
            size="small"
            color="error"
            aria-label="Expire away location"
            title="Expire away location"
            @click="handleOpenExpireModal(item)"
          >
            <v-icon :icon="mdiClockRemove" />
          </UaBtn>
        </div>
      </template>
    </UaDataTable>

    <UaPlaceholderPage
      v-else-if="!isFetchingAwayLocations && !awayLocationsError"
      title="No away locations"
      description="No away locations have been assigned."
    />

    <AwayLocationModal
      v-if="showAwayLocationModal"
      :user-id="userId"
      :locations="locationsStore.selectOptions"
      :away-location="selectedAwayLocation"
      @close="handleModalClose"
      @saved="handleSaved"
    />

    <ExpireAwayLocationModal
      v-if="showExpireModal && selectedExpireAwayLocation"
      :user-id="userId"
      :away-location="selectedExpireAwayLocation"
      @close="handleExpireModalClose"
      @expired="handleExpired"
    />
  </div>
</template>

<style scoped>
.away-locations {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.away-locations-header {
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
