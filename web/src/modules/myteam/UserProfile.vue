<script setup lang="ts">
import { getApiUsersId } from '@/api-access/generated/users/users';

const props = defineProps<{
  userId: string;
}>();

const { data, error, isFetching } = getApiUsersId(props.userId);
</script>

<template>
  <div v-if="isFetching">Loading ...</div>
  <div v-else-if="error">Error: {{ error.message }}</div>
  <h2 style="margin-left: 4rem">Profile</h2>
  <div style="display: flex; gap: 1rem; background-color: #fff; padding: 2rem">
    <!-- Left Panel -->
    <div class="left-panel">
      <div style="border: 1px solid rgba(var(--v-theme-surface-light)); padding: 2rem">
        <v-avatar color="brown" size="80">
          <span class="text-headline-small">{{ data?.firstName?.[0] || '' }}{{ data?.lastName?.[0] || '' }}</span>
        </v-avatar>
      </div>

      <div>
        <div>{{ data?.firstName }} {{ data?.lastName }}</div>
        <div>Chief Sheriff</div>
        <div>{{ data?.badgeNumber }}</div>
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
    <div class="right-panel">
      <RouterView v-if="data" v-slot="{ Component }">
        <component :is="Component" :user="data" />
      </RouterView>
    </div>
  </div>
</template>

<style scoped>
.left-panel {
  display: flex;
  flex-direction: column;
  gap: 2rem;
  align-items: center;
  margin-right: 4rem;
}

.right-panel {
  flex: 1;
  background-color: rgba(var(--v-theme-surface-light));
  padding: 0.5rem 2rem;
}
</style>
