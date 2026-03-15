<script setup lang="ts">
import { computed } from 'vue';
import { useRouter } from 'vue-router';
import type { UserResponse } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';

const { user } = defineProps<{
  user: Partial<UserResponse>;
}>();

const router = useRouter();

const initials = computed(() => `${user.firstName?.[0] || ''}${user.lastName?.[0] || ''}`);
const fullName = computed(() => `${user.firstName || ''} ${user.lastName || ''}`.trim());

const gotoProfile = () => {
  router.push({ name: 'UserProfile', params: { userId: user.id } });
};
</script>

<template>
  <v-card class="user-card" @click="gotoProfile">
    <v-avatar color="grey" size="40">
      <span class="text-headline-small">{{ initials }}</span>
    </v-avatar>
    <v-card-title :title="fullName" class="user-card-title">
      <div class="user-full-name">
        {{ fullName }}
      </div>
      <div style="font-size: 0.8rem; text-align: center">Chief Sheriff</div>
      <div v-if="useAccessControl().isFeatureFlagEnabled('userBadgeNumber')"
        style="font-size: 0.8rem; text-align: center">
        {{ user.badgeNumber }}
      </div>
    </v-card-title>
  </v-card>
</template>

<style scoped>
.user-card {
  width: 160px;
  height: 160px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  background-color: rgba(var(--v-theme-surface-bright), 1);
  padding: 1rem;
}

.user-card-title {
  font-size: 1rem;
  width: 100%;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.user-fullname {
  text-overflow: ellipsis;
  white-space: nowrap;
  overflow: hidden;
  font-weight: bold;
}
</style>
