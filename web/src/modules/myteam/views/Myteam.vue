<script setup lang="ts">
import { Permissions, type GetApiUsersParams } from '@/api-access/generated/models';
import { getApiUsers } from '@/api-access/generated/users/users';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaPageHeader from '@/shared/components/UaPageHeader.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import { mdiPlus } from '@mdi/js';
import { computed, ref, watch } from 'vue';
import UserCard from '../components/UserCard.vue';
import UserFormModal from '../components/UserFormModal.vue';
import { useAccessControl } from '@/composables/useAccessControl';
import { useLocationsStore } from '@/stores/LocationsStore';

const accessControl = useAccessControl();
const locationsStore = useLocationsStore();

const searchText = ref('');
const isEnabled = ref<boolean | undefined>(true);

// Make params reactive using computed - `useFetchAPI` needs this to read the lastest queryParam values when queryParams change
const searchParams = computed(() => {
  const params: GetApiUsersParams = {};
  if (searchText.value) {
    params.Search = searchText.value;
  }
  if (isEnabled.value !== undefined) {
    params.IsEnabled = isEnabled.value;
  }
  const locationId = locationsStore.selectedLocationId;
  const parsedLocationId = locationId === '' || locationId == null ? null : Number(locationId);
  if (parsedLocationId != null && !Number.isNaN(parsedLocationId)) {
    params.LocationId = parsedLocationId;
  }
  return params;
});

// Call the execute function when you want to re run api call with updated search params
const { data, error, isFetching, execute } = getApiUsers(searchParams);

watch(
  () => locationsStore.selectedLocationId,
  () => execute(),
);

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

  <div class="my-team-list-header">
    <div class="my-team-search-wrapper">
      <UaTextField
        id="team-search"
        label="Search"
        v-model="searchText"
        class="my-team-search-input"
        @keydown.enter="() => execute()"
      />
    </div>
    <div class="my-team-filter-row">
      <label> In Active </label>
      <v-switch inset v-model="isEnabled" color="primary" hide-details @update:model-value="() => execute()"></v-switch>
      <label> Active </label>
    </div>
  </div>

  <div class="my-team-list-container">
    <div v-if="isFetching">Loading ...</div>
    <div v-else-if="error">Error: {{ error.message }}</div>
    <UserCard v-else v-for="user in data" :key="user.id" :user="user" />
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
.my-team-list-header {
  padding: var(--ua-spacing-xl);
  background-color: rgb(var(--v-theme-surface));
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--ua-spacing-md);
}

.my-team-search-wrapper {
  width: 320px;
}

.my-team-search-input {
  max-width: 400px;
}

.my-team-filter-row {
  display: flex;
  gap: var(--ua-spacing-md);
  align-items: center;
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
