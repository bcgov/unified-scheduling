<script setup lang="ts">
import { useAccessControl } from '@/composables/useAccessControl';

const accessControl = useAccessControl();
</script>

<template>
  <v-app-bar style="background-color: #eaeaea; padding: 0.5rem 2rem" density="compact">
    <div style="margin-left: 2rem">
      <img width="132" src="../../assets/images/bcid-logo-en.svg" alt="" />
    </div>
    <div style="display: flex; gap: 2rem; margin-left: 8rem">
      <RouterLink class="router-link" to="/dashboard" active-class="active">Dashboard</RouterLink>
      <RouterLink class="router-link router-link--border" v-if="accessControl.isFeatureFlagEnabled('schedulingModule')"
        to="/schedule" active-class="active">
        Schedule
      </RouterLink>
      <RouterLink class="router-link router-link--border" v-if="accessControl.isFeatureFlagEnabled('myteamsModule')"
        to="/myteam" active-class="active">
        My Team
      </RouterLink>
    </div>
  </v-app-bar>
</template>

<style>
.router-link {
  color: rgba(var(--v-theme-on-surface), 0.87);
  padding-right: 2rem;
  text-decoration: none;
}

.router-link--border {
  border-right: 2px solid #333;
}

.router-link.active,
.router-link:hover {
  font-weight: 500;
  color: rgba(var(--v-theme-primary), 1);
  text-decoration: underline;
}
</style>
