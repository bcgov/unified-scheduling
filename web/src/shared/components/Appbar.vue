<script setup lang="ts">
import { useNavigationStore } from '@/stores/NavigationStore';

const navigationStore = useNavigationStore();
</script>

<template>
  <v-app-bar class="app-bar" density="compact">
    <div style="margin-left: var(--ua-spacing-xl)">
      <img width="132" src="../../assets/images/bcid-logo-en.svg" alt="" />
    </div>
    <div class="router-link-container">
      <RouterLink
        v-for="navItem in navigationStore.links"
        :key="navItem.name"
        :class="['router-link', navItem?.class ?? '']"
        :to="navItem.path"
        active-class="active"
      >
        {{ navItem.name }}
      </RouterLink>
    </div>
  </v-app-bar>
</template>

<style>
.app-bar {
  background-color: rgb(var(--v-theme-surface-light));
  padding: var(--ua-spacing-sm) var(--ua-spacing-xl);
}

.router-link-container {
  display: flex;
  gap: var(--ua-spacing-xl);
  margin-left: 8rem;
}

.router-link {
  color: var(--ua-text-primary);
  padding-left: var(--ua-spacing-xl);
  text-decoration: none;
}

.router-link--border {
  border-left: 2px solid var(--ua-text-primary);
}

.router-link.active,
.router-link:hover {
  font-weight: var(--ua-font-weight-semibold);
  color: rgb(var(--v-theme-primary));
  text-decoration: underline;
}
</style>
