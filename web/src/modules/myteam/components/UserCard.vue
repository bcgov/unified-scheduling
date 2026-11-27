<script setup lang="ts">
import { resolveApiUrl } from '@/api-access/useFetchAPI';
import UaCard from '@/shared/components/UaCard.vue';
import { mdiTimerSand } from '@mdi/js';
import { computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { LookupCodeTypes, type UserResponse } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import { useLookupStore } from '@/stores/LookupStore';
import { useLocationsStore } from '@/stores/LocationsStore';

const { user } = defineProps<{
  user: Partial<UserResponse> & { pendingRegistration?: boolean };
}>();

const router = useRouter();
const lookupStore = useLookupStore();
const locationsStore = useLocationsStore();
const accessControl = useAccessControl();

const initials = computed(() => `${user.firstName?.[0] || ''}${user.lastName?.[0] || ''}`);
const fullName = computed(() => `${user.firstName || ''} ${user.lastName || ''}`.trim());
const positionDescription = computed(() => {
  if (!user?.rank) {
    return '-';
  }

  return lookupStore.entityMap[LookupCodeTypes.PositionTypes]?.[user.rank]?.description ?? '-';
});

const locationName = computed(() =>
  user.homeLocationId == null ? '' : (locationsStore.entitiesMap[user.homeLocationId]?.name ?? ''),
);

const gotoProfile = () => {
  router.push({ name: 'UserProfile', params: { userId: user.id } });
};

onMounted(async () => {
  try {
    await lookupStore.load(LookupCodeTypes.PositionTypes);
  } catch {
    // Keep the existing placeholder until a later lookup succeeds.
  }
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
        :src="`${resolveApiUrl(user.photoUrl)}?t=${user.lastPhotoUpdate ?? user.id}`"
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
        v-if="accessControl.featureFlags.value?.UserManagement?.userBadgeNumber?.enabled"
        style="font-size: var(--ua-font-size-sm); text-align: center"
      >
        {{ user.badgeNumber }}
      </div>
      <div v-if="locationName" class="user-card-location">{{ locationName }}</div>
    </div>
  </UaCard>
</template>

<style scoped>
.user-card {
  position: relative;
  width: 230px;
  min-height: 200px;
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
  flex-shrink: 0;
  font-size: var(--ua-font-size-base);
  width: 100%;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  text-align: center;
}

.user-card-location {
  font-size: var(--ua-font-size-sm);
  text-align: center;
  white-space: normal;
  overflow-wrap: anywhere;
  word-break: break-word;
}

.user-fullname {
  text-overflow: ellipsis;
  white-space: nowrap;
  overflow: hidden;
  font-weight: var(--ua-font-weight-bold);
}
</style>
