<script setup lang="ts">
import type { UserResponse } from '@/api-access/generated/models';

const { user } = defineProps<{
  user: UserResponse;
}>();
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
    <div>{{ user?.idirId }}</div>

    <label class="identification-label">Email</label>
    <div>{{ user?.email }}</div>

    <label class="identification-label">Gender</label>
    <div>Female</div>

    <label class="identification-label">Badge Number</label>
    <div>{{ user?.badgeNumber }}</div>

    <label class="identification-label">Rank</label>
    <div>Rank</div>

    <label class="identification-label">Location</label>
    <div>Location</div>

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
