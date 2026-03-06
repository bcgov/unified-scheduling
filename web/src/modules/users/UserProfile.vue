<script setup lang="ts">
import { getApiUsersId } from '@/api-access/generated/users/users';
// import { useUsersStore } from '@/stores/Users';

const props = defineProps<{
  userId: string;
}>();

const { data, error, isFetching } = getApiUsersId(props.userId);

// const usersStore = useUsersStore();
const user = data; // usersStore.entitiesMap[props.userId];
</script>

<template>
  <div v-if="isFetching">Loading...</div>
  <div v-else-if="error">Error: {{ error.message }}</div>
  <h2 style="margin-left: 4rem">Profile</h2>
  <div style="display: flex; gap: 1rem; background-color: #fff; padding: 2rem">
    <!-- Left Panel -->
    <div style="display: flex; flex-direction: column; gap: 2rem; align-items: center; margin-right: 4rem">
      <div style="border: 1px solid rgba(var(--v-theme-surface-light)); padding: 2rem;">
        <v-avatar color="brown" size="80">
          <span class="text-headline-small">{{ user?.firstName?.[0] || '' }}{{ user?.lastName?.[0] || '' }}</span>
        </v-avatar>
      </div>

      <div>
        <div>{{ user?.firstName }} {{ user?.lastName }}</div>
        <div>Chief Sheriff</div>
        <div>9909887</div>
      </div>

      <div style="display: flex; flex-direction: column; gap: 1rem; width: 100%">
        <v-btn variant="outlined">Identification</v-btn>
        <v-btn variant="outlined">Acting rank</v-btn>
        <v-btn variant="outlined">Schedule</v-btn>
        <v-btn variant="outlined">Work History</v-btn>
        <v-btn variant="outlined">Schedule</v-btn>
        <v-btn variant="outlined">Deactivate</v-btn>
      </div>
    </div>
    <!-- Right Panel -->
    <div style="flex: 1;background-color: rgba(var(--v-theme-surface-light)); padding: .5rem 2rem;">
      <h3>Identification</h3>
      <div style="display: grid; grid-template-columns: max-content 1fr; gap: 1rem 6rem; margin-top: 1rem;">
        <!-- Label on left, value on right - auto-flows into rows -->
        <label style="font-weight: bold;">First Name</label>
        <div>{{ user?.firstName }}</div>

        <label style="font-weight: bold;">Last Name</label>
        <div>{{ user?.lastName }}</div>

        <label style="font-weight: bold;">IDIR</label>
        <div>{{ user?.idirId }}</div>

        <label style="font-weight: bold;">Email</label>
        <div>{{ user?.email }}</div>

        <label style="font-weight: bold;">Gender</label>
        <div>Female</div>

        <label style="font-weight: bold;">Rank</label>
        <div>Rank</div>

        <label style="font-weight: bold;">Location</label>
        <div>Location</div>

        <label style="font-weight: bold;">Role</label>
        <div>Role</div>

        <!-- Add more label/value pairs - they will automatically flow into new rows -->
      </div>
    </div>
  </div>
</template>
