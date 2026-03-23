<script setup lang="ts">
import { computed } from 'vue';
import { getApiUsersId } from '@/api-access/generated/users/users';
import { useAccessControl } from '@/composables/useAccessControl';

const props = defineProps<{
  userId: string;
}>();

const { data, error, isFetching } = getApiUsersId(props.userId);
const accessControl = useAccessControl();
const showBadgeNumber = computed(() => accessControl.isFeatureFlagEnabled('userBadgeNumber'));

</script>

<template>
  <div v-if="isFetching">Loading ...</div>
  <div v-else-if="error">Error: {{ error.message }}</div>
  <h2 class="profile-title">Profile</h2>
  <div class="profile-layout">
    <!-- Left Panel -->
    <div class="left-panel">
      <div class="avatar-container">
        <v-avatar color="brown" size="80">
          <span class="text-headline-small">{{ data?.firstName?.[0] || '' }}{{ data?.lastName?.[0] || '' }}</span>
        </v-avatar>
      </div>

      <div>
        <div>{{ data?.firstName }} {{ data?.lastName }}</div>
        <div>Chief Sheriff</div>
        <div v-if="showBadgeNumber">{{ data?.badgeNumber }}</div>
      </div>

      <div class="profile-subnav">
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
.profile-title {
  margin-left: 4rem;
}

.profile-layout {
  display: flex;
  gap: 1rem;
  background-color: #fff;
  padding: 2rem;
}

.left-panel {
  display: flex;
  flex-direction: column;
  gap: 2rem;
  align-items: center;
  margin-right: 4rem;
}

.avatar-container {
  border: 1px solid rgba(var(--v-theme-surface-light));
  padding: 2rem;
}

.profile-subnav {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  width: 100%;
}

.right-panel {
  flex: 1;
  background-color: rgba(var(--v-theme-surface-light));
  padding: 0.5rem 2rem;
}
</style>
