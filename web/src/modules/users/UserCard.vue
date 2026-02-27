<script setup lang="ts">
import type { User } from '@/api-access/generated/models';
import { computed } from 'vue';
import { useRouter } from 'vue-router';

const router = useRouter();
const props = defineProps<{
  user: Partial<User>;
}>();

const initials = computed(() => `${props.user.firstName?.[0] || ''}${props.user.lastName?.[0] || ''}`);
const fullName = computed(() => `${props.user.firstName || ''} ${props.user.lastName || ''}`.trim());

const gotoProfile = () => {
  // Navigate to user profile page
  // This can be implemented using Vue Router's useRouter() and router.push() methods
  router.push({ name: 'UserProfile', params: { userId: props.user.id } });
};
</script>
<template>
  <v-card class="u-user-card" @click="gotoProfile">
    <v-avatar color="grey" size="40">
      <span class="text-headline-small">{{ initials }}</span>
    </v-avatar>
    <v-card-title :title="fullName"
      style="font-size: 1rem; width: 100%; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">
      {{ fullName }}
    </v-card-title>
  </v-card>
</template>

<style scoped>
.u-user-card {
  width: 160px;
  height: 160px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  background-color: rgba(var(--v-theme-surface-bright), 1);
  padding: 1rem;
}
</style>
