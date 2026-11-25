<script setup lang="ts">
import { Permissions } from '@/api-access/generated/models';
import { getApiUsersPaged, type PagedUsersQuery } from '../api/usersPagedApi';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaCard from '@/shared/components/UaCard.vue';
import UaPageHeader from '@/shared/components/UaPageHeader.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import { mdiPlus } from '@mdi/js';
import UaDataTableServer from '@/shared/components/UaDataTableServer.vue';
import UaAutocomplete from '@/shared/components/UaAutocomplete.vue';
import type { SelectCode } from '@/types/select';
import { computed, onMounted, ref, watch } from 'vue';
import UserCard from '../components/UserCard.vue';
import UserFormModal from '../components/UserFormModal.vue';
import { useAccessControl } from '@/composables/useAccessControl';
import { useLocationsStore } from '@/stores/LocationsStore';

const DEFAULT_PAGE_SIZE = 10;
const PAGE_SIZE_OPTIONS = [
  { value: 10, title: '10' },
  { value: 25, title: '25' },
  { value: 50, title: '50' },
];

const accessControl = useAccessControl();
const locationsStore = useLocationsStore();

const searchText = ref('');
const isEnabled = ref<boolean | undefined>(true);
// Starts from the app-bar location but can be changed independently for this search
const selectedLocationIds = ref<number[]>(toLocationIds(locationsStore.selectedLocationId));
const page = ref(1);
const pageSize = ref(DEFAULT_PAGE_SIZE);

function toLocationIds(id: SelectCode | null): number[] {
  const parsed = id === '' || id == null ? NaN : Number(id);
  return Number.isNaN(parsed) ? [] : [parsed];
}

// Make params reactive using computed - `useFetchAPI` needs this to read the lastest queryParam values when queryParams change
const searchParams = computed(() => {
  const params: PagedUsersQuery = { Page: page.value, PageSize: pageSize.value };
  if (searchText.value) {
    params.Search = searchText.value;
  }
  if (isEnabled.value !== undefined) {
    params.IsEnabled = isEnabled.value;
  }
  if (selectedLocationIds.value.length > 0) {
    params.LocationId = selectedLocationIds.value;
  }
  return params;
});

// Call the execute function when you want to re run api call with updated search params
const { data: pagedData, error, isFetching, execute } = getApiUsersPaged(searchParams);

const users = computed(() => pagedData.value?.data ?? []);
const totalCount = computed(() => pagedData.value?.totalCount ?? 0);

// Any filter change starts again from the first page.
const search = async () => {
  page.value = 1;
  await execute();
};

const goToPage = async (newPage: number) => {
  page.value = newPage;
  await execute();
};

const updatePageSize = async (newPageSize: number) => {
  pageSize.value = newPageSize;
  await search();
};

watch(
  () => locationsStore.selectedLocationId,
  (id) => {
    selectedLocationIds.value = toLocationIds(id);
  },
);

watch(selectedLocationIds, () => search());

onMounted(() => {
  if (!locationsStore.selectOptions.length) {
    locationsStore.getEntities();
  }
});

const handleClearSearch = async () => {
  searchText.value = '';
  await search();
};

const showCreateUserModal = ref(false);

const handleAddMember = () => {
  showCreateUserModal.value = true;
};

const handleUserCreated = async () => {
  // Refresh the user list after creating a new user
  await execute();
};

const handleUserUpdated = async () => {
  // Refresh the user list after updating a user
  await execute();
};

const handleCreateModalClose = () => {
  showCreateUserModal.value = false;
};
</script>

<template>
  <UaPageHeader title="My Team">
    <template #actions>
      <UaBtn
        v-if="accessControl.hasPermission(Permissions.UsersCreate)"
        @click="handleAddMember"
        :prepend-icon="mdiPlus"
        >Add Member</UaBtn
      >
    </template>
  </UaPageHeader>

  <div class="my-team-filters">
    <UaCard title="Filters">
      <div class="filters-grid">
        <div class="filter-field">
          <label class="filter-label" for="team-search">Search by name or badge number</label>
          <UaTextField
            id="team-search"
            label=""
            hint="Enter a first name, last name, or badge number, then press Enter."
            persistent-hint
            v-model="searchText"
            clearable
            @click:clear="handleClearSearch"
            @keydown.enter="search"
          />
        </div>
        <div v-if="accessControl.hasPermission(Permissions.UserGlobalSearch)" class="filter-field">
          <label class="filter-label" for="team-location">Location</label>
          <UaAutocomplete
            id="team-location"
            v-model="selectedLocationIds"
            :items="locationsStore.selectOptions"
            client-filter
            browser-autocomplete="off"
            density="compact"
            variant="outlined"
            label=""
            multiple
            chips
            closable-chips
            clearable
          />
        </div>
        <div class="filter-field">
          <span class="filter-label">Status</span>
          <div class="my-team-filter-row">
            <label> In Active </label>
            <v-switch inset v-model="isEnabled" color="primary" hide-details @update:model-value="search"></v-switch>
            <label> Active </label>
          </div>
        </div>
      </div>
    </UaCard>
  </div>

  <div class="my-team-list-container">
    <div v-if="isFetching">Loading ...</div>
    <div v-else-if="error">Error: {{ error.message }}</div>
    <UserCard v-else v-for="user in users" :key="user.id" :user="user" />
  </div>

  <!-- Footer-only server table so the pager matches the Audit Log's look and feel -->
  <div class="my-team-pagination">
    <UaDataTableServer
      :headers="[]"
      :items="[]"
      :items-length="totalCount"
      :page="page"
      :items-per-page="pageSize"
      :items-per-page-options="PAGE_SIZE_OPTIONS"
      :loading="isFetching"
      hide-default-header
      hide-no-data
      @update:page="goToPage"
      @update:items-per-page="updatePageSize"
    />
  </div>
  <!-- Create User Modal -->
  <UserFormModal
    v-if="showCreateUserModal"
    @close="handleCreateModalClose"
    @created="handleUserCreated"
    @updated="handleUserUpdated"
  />
</template>

<style scoped>
.my-team-filters {
  padding: var(--ua-spacing-xl) var(--ua-spacing-xl) 0;
}

.filters-grid {
  display: grid;
  grid-template-columns: minmax(220px, 1fr) minmax(220px, 1fr) minmax(200px, 0.5fr);
  gap: var(--ua-spacing-xl);
  align-items: start;
}

.filter-field {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xs);
}

.filter-label {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
}

@media (max-width: 900px) {
  .filters-grid {
    grid-template-columns: 1fr;
  }
}

.my-team-filter-row {
  display: flex;
  gap: var(--ua-spacing-md);
  align-items: center;
  min-height: 40px;
}

.my-team-pagination {
  padding: 0 var(--ua-spacing-xl) var(--ua-spacing-xl);
}

.my-team-pagination :deep(.ua-data-table) {
  border: none;
  background-color: transparent;
}

.my-team-list-container {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-xl);
  justify-content: flex-start;
  align-items: flex-start;
  padding: var(--ua-spacing-xl);
}
</style>
