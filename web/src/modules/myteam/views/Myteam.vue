<script setup lang="ts">
import { ref, computed } from 'vue';
import { mdiPlus } from '@mdi/js';
import { getApiUsers } from '@/api-access/generated/users/users';
import type { GetApiUsersParams } from '@/api-access/generated/models';
import UserCard from '../components/UserCard.vue';
import UserFormModal from '../components/UserFormModal.vue';

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
  return params;
});

// Call the execute function when you want to re run api call with updated search params
const { data, error, isFetching, execute } = getApiUsers(searchParams);

const showCreateUserModal = ref(false);

const handleAddMember = () => {
  showCreateUserModal.value = true;
};

const handleUserCreated = async () => {
  // Refresh the user list after creating a new user
  await execute();
};

const handleCreateModalClose = () => {
  showCreateUserModal.value = false;
};
</script>

<template>
  <div class="my-team-header-row">
    <div>
      <h2 class="my-team-title">My Team</h2>
    </div>
    <div>
      <v-btn @click="handleAddMember"> <v-icon :icon="mdiPlus"></v-icon> Add Member </v-btn>
    </div>
  </div>

  <div class="my-team-list-header">
    <div class="my-team-search-wrapper">
      <v-text-field
        v-model="searchText"
        placeholder="Search"
        variant="outlined"
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
  <UserFormModal v-if="showCreateUserModal" @close="handleCreateModalClose" @created="handleUserCreated" />
</template>

<style scoped>
.my-team-header-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.my-team-title {
  margin-left: 4rem;
}

.my-team-list-header {
  padding: 2rem;
  background-color: #fff;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.my-team-search-wrapper {
  width: 320px;
}

.my-team-search-input {
  max-width: 400px;
}

.my-team-filter-row {
  display: flex;
  gap: 1rem;
  align-items: center;
}

.my-team-list-container {
  display: flex;
  flex-wrap: wrap;
  gap: 2rem;
  justify-content: flex-start;
  align-items: flex-start;
  padding: 2rem;
}
</style>
