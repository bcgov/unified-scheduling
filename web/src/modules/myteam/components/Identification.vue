<script setup lang="ts">
import { computed } from 'vue';
import type { UserResponse } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import { useLocationsStore } from '@/stores/LocationsStore';

const { user } = defineProps<{
  user: UserResponse;
}>();

const accessControl = useAccessControl();
const locationsStore = useLocationsStore();
const showBadgeNumber = computed(() => accessControl.isFeatureFlagEnabled('userBadgeNumber'));

const locationName = computed(() => {
  if (user?.homeLocationId == null) {
    return '-';
  }

  return locationsStore.entitiesMap[user.homeLocationId]?.name ?? '-';
});

</script>
<template>
  <h3>Identification</h3>
  <div class="identification-grid">
    <!-- Label on left, value on right - auto-flows into rows -->
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
    <div>{{ user?.rank }}</div>

    <label class="identification-label">Location</label>
    <div>{{ locationName }}</div>

    <label class="identification-label">Role</label>
    <div>Role</div>

    <!-- Add more label/value pairs - they will automatically flow into new rows -->
  </div>
</template>

<style scoped>
.identification-grid {
  display: grid;
  grid-template-columns: max-content 1fr;
  gap: 1rem 6rem;
  margin-top: 1rem;
}

.identification-label {
  font-weight: bold;
}
</style>
