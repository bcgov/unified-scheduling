<script setup lang="ts">
import { LookupCodeTypes, type UserResponse } from '@/api-access/generated/models';
import { mdiTimerSand } from '@mdi/js';
import { useAccessControl } from '@/composables/useAccessControl';
import { useLocationsStore } from '@/stores/LocationsStore';
import { useLookupStore } from '@/stores/LookupStore';
import { computed, onMounted } from 'vue';

const { user } = defineProps<{
  user: UserResponse & { pendingRegistration?: boolean };
}>();

const accessControl = useAccessControl();
const locationsStore = useLocationsStore();
const lookupStore = useLookupStore();
const showBadgeNumber = computed(() => accessControl.isFeatureFlagEnabled('userBadgeNumber'));

const locationName = computed(() => {
  if (user?.homeLocationId == null) {
    return '-';
  }

  return locationsStore.entitiesMap[user.homeLocationId]?.name ?? '-';
});

const positionDescription = computed(() => {
  if (user?.rank == null) {
    return '-';
  }

  return lookupStore.getDescriptionFromCode(LookupCodeTypes.PositionTypes, user.rank);
});

onMounted(async () => {
  await lookupStore.load(LookupCodeTypes.PositionTypes);
});
</script>
<template>
  <div class="identification-header">
    <h3>Identification</h3>
    <v-icon
      v-if="user.pendingRegistration"
      :icon="mdiTimerSand"
      color="warning"
      size="20"
      title="Pending registration"
    />
  </div>
  <div class="identification-grid">
    <label class="identification-label">First Name</label>
    <div>{{ user?.firstName }}</div>

    <label class="identification-label">Last Name</label>
    <div>{{ user?.lastName }}</div>

    <label class="identification-label">IDIR</label>
    <div>{{ user?.idirName }}</div>

    <label class="identification-label">Email</label>
    <div>{{ user?.email }}</div>

    <label class="identification-label">Gender</label>
    <div>{{ user?.gender }}</div>

    <template v-if="showBadgeNumber">
      <label class="identification-label">Badge Number</label>
      <div>{{ user?.badgeNumber }}</div>
    </template>

    <label class="identification-label">Rank</label>
    <div>{{ positionDescription }}</div>

    <label class="identification-label">Location</label>
    <div>{{ locationName }}</div>

    <label class="identification-label">Role</label>
    <div>Role</div>
  </div>
</template>

<style scoped>
.identification-header {
  display: flex;
  align-items: center;
  gap: var(--ua-spacing-sm);
}

.identification-grid {
  display: grid;
  grid-template-columns: max-content 1fr;
  gap: var(--ua-spacing-md) var(--ua-spacing-2xl);
  margin-top: var(--ua-spacing-md);
}

.identification-label {
  font-weight: var(--ua-font-weight-bold);
}
</style>
