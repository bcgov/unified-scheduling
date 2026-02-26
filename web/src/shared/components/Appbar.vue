<script setup lang="ts">
import { shallowRef } from 'vue';
import { Modules } from '@/stores/config';
import { useAccessControl } from '@/composables/useAccessControl';

const accessControl = useAccessControl();

const modules = shallowRef(Modules);

</script>

<template>
  <v-app-bar style="background-color: #eaeaea; padding: 0.5rem 2rem;" density="compact">
    <div style="margin-left: 2rem;">
      <img width="132" src="../../assets/images/bcid-logo-en.svg" alt="">
    </div>
    <div style="display: flex; gap: 2rem; margin-left: 8rem;">
      <RouterLink class="u-router-link u-router-link--border" v-if="accessControl.canAccessModule(modules.scheduling)"
        to="/scheduling" active-class="active">
        Schedule</RouterLink>
      <RouterLink class="u-router-link u-router-link--border" v-if="accessControl.canAccessModule(modules.users)"
        to="/users" active-class="active">My Team
      </RouterLink>
      <RouterLink class="u-router-link" to="/dashboard" active-class="active">Dashboard</RouterLink>
    </div>
  </v-app-bar>
</template>

<style>
.u-router-link {
  color: rgba(var(--v-theme-on-surface), 0.87);
  padding-right: 2rem;
  text-decoration: none;
}

.u-router-link--border {
  border-right: 2px solid #333
}

.u-router-link.active,
.u-router-link:hover {
  font-weight: 500;
  color: rgba(var(--v-theme-primary), 1);
  text-decoration: underline;
}
</style>
