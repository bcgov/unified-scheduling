<script setup lang="ts">
import type { UserResponse } from '@/api-access/generated/models';

const { user } = defineProps<{
  user: UserResponse;
}>();
</script>
<template>
  <h3>Identification</h3>
  <div style="display: grid; grid-template-columns: max-content 1fr; gap: 1rem 6rem; margin-top: 1rem">
    <!-- Label on left, value on right - auto-flows into rows -->
    <label style="font-weight: bold">First Name</label>
    <div>{{ user?.firstName }}</div>

    <label style="font-weight: bold">Last Name</label>
    <div>{{ user?.lastName }}</div>

    <label style="font-weight: bold">IDIR</label>
    <div>{{ user?.idirId }}</div>

    <label style="font-weight: bold">Email</label>
    <div>{{ user?.email }}</div>

    <label style="font-weight: bold">Gender</label>
    <div>Female</div>

    <label style="font-weight: bold">Badge Number</label>
    <div>{{ user?.badgeNumber }}</div>

    <label style="font-weight: bold">Rank</label>
    <div>Rank</div>

    <label style="font-weight: bold">Location</label>
    <div>Location</div>

    <label style="font-weight: bold">Role</label>
    <div>Role</div>

    <!-- Add more label/value pairs - they will automatically flow into new rows -->
  </div>
</template>
