<script setup lang="ts">
import UaCard from '@/shared/components/UaCard.vue';
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
  <UaCard class="user-card" @click="gotoProfile">
    <v-avatar color="grey" size="40">
      <span class="text-headline-small">{{ initials }}</span>
    </v-avatar>
    <div :title="fullName" class="user-card-title">
      <div class="user-full-name">
        {{ fullName }}
      </div>
      <div style="font-size: var(--ua-font-size-sm); text-align: center">Chief Sheriff</div>
      <div
        v-if="useAccessControl().isFeatureFlagEnabled('userBadgeNumber')"
        style="font-size: var(--ua-font-size-sm); text-align: center"
      >
        {{ user.badgeNumber }}
      </div>
    </div>
  </UaCard>
</template>

<style scoped>
.user-card {
  width: 160px;
  height: 160px;
  cursor: pointer;
}

:deep(.ua-card__body) {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  padding: var(--ua-spacing-md);
}

.user-card-title {
  font-size: var(--ua-font-size-base);
  width: 100%;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.user-fullname {
  text-overflow: ellipsis;
  white-space: nowrap;
  overflow: hidden;
  font-weight: var(--ua-font-weight-bold);
}
</style>
