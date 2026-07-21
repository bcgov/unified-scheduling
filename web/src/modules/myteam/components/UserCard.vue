<script setup lang="ts">
import UaCard from '@/shared/components/UaCard.vue';
import { mdiTimerSand } from '@mdi/js';
import { computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { LookupCodeTypes, type UserResponse } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import { useLookupStore } from '@/stores/LookupStore';

const { user } = defineProps<{
  user: Partial<UserResponse> & { pendingRegistration?: boolean };
}>();

const router = useRouter();
const lookupStore = useLookupStore();

const initials = computed(() => `${user.firstName?.[0] || ''}${user.lastName?.[0] || ''}`);
const fullName = computed(() => `${user.firstName || ''} ${user.lastName || ''}`.trim());
const positionDescription = computed(() => {
  if (!user?.rank) {
    return '-';
  }

  return lookupStore.entityMap[LookupCodeTypes.PositionTypes]?.[user.rank]?.description ?? '-';
});

const gotoProfile = () => {
  router.push({ name: 'UserProfile', params: { userId: user.id } });
};

onMounted(async () => {
  await lookupStore.load(LookupCodeTypes.PositionTypes);
});
</script>

<template>
  <UaCard class="user-card" @click="gotoProfile">
    <div v-if="user.pendingRegistration" class="user-card-pending-icon" title="Pending registration">
      <v-icon :icon="mdiTimerSand" color="warning" size="20" />
    </div>
    <v-avatar color="grey" size="80">
      <v-img
        v-if="user.photoUrl"
        :src="`${user.photoUrl}?t=${user.lastPhotoUpdate ?? user.id}`"
        cover
        :alt="initials"
      />
      <span v-else class="text-headline-small">{{ initials }}</span>
    </v-avatar>
    <div :title="fullName" class="user-card-title">
      <div class="user-full-name">
        {{ fullName }}
      </div>
      <div style="font-size: var(--ua-font-size-sm); text-align: center">{{ positionDescription }}</div>
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
  position: relative;
  width: 180px;
  height: 180px;
  cursor: pointer;
}

.user-card-pending-icon {
  position: absolute;
  top: var(--ua-spacing-sm);
  right: var(--ua-spacing-sm);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1;
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
  text-align: center;
}

.user-fullname {
  text-overflow: ellipsis;
  white-space: nowrap;
  overflow: hidden;
  font-weight: var(--ua-font-weight-bold);
}
</style>
