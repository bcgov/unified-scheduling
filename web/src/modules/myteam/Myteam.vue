<script setup lang="ts">
import { ref, computed } from 'vue';
import UserCard from './components/UserCard.vue';
import { getApiUsers } from '@/api-access/generated/users/users';
import type { GetApiUsersParams } from '@/api-access/generated/models';

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
</script>

<template>
  <div style="display: flex; align-items: center; justify-content: space-between">
    <div>
      <h2 style="margin-left: 4rem">My Team</h2>
    </div>
  </div>

  <div class="my-team-list-header">
    <div style="width: 320px">
      <v-text-field
        v-model="searchText"
        placeholder="Search"
        density="compact"
        variant="outlined"
        style="max-width: 400px"
        @keydown.enter="() => execute()"
      />
    </div>
    <div style="display: flex; gap: 1rem; align-items: center">
      <label> In Active </label>
      <v-switch
        inset
        v-model="isEnabled"
        color="primary"
        hide-details
        density="compact"
        @update:model-value="() => execute()"
      ></v-switch>
      <label> Active </label>
    </div>
  </div>

  <div class="my-team-list-container">
    <div v-if="isFetching">Loading ...</div>
    <div v-else-if="error">Error: {{ error.message }}</div>
    <UserCard v-else v-for="user in data" :key="user.id" :user="user" />
  </div>
</template>

<style scoped>
.my-team-list-header {
  padding: 2rem;
  background-color: #fff;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
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
