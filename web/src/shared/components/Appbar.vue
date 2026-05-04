<script setup lang="ts">
import { useNavigationStore } from '@/stores/NavigationStore';
const bcgovLogo = new URL('/images/bcid-logo-rev-en.svg', import.meta.url).href;
const navigationStore = useNavigationStore();
</script>

<template>
  <div class="app-bar-wrapper">
    <v-app-bar class="app-bar" density="compact" flat>
      <div style="margin-left: var(--ua-spacing-xl)">
        <img width="177" height="44" :src="bcgovLogo" alt="B.C. Government Logo" />
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
    <div class="gold-accent-bar" />
  </div>
</template>

<style>
.app-bar-wrapper {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: 1000;
}

.app-bar {
  background-color: rgb(var(--v-theme-primary)) !important;
  padding: var(--ua-spacing-sm) var(--ua-spacing-xl);
  position: static !important;
}

.gold-accent-bar {
  height: 4px;
  background-color: rgb(var(--v-theme-accent));
}

.router-link-container {
  display: flex;
  gap: var(--ua-spacing-xl);
  margin-left: 8rem;
}

.router-link {
  color: var(--ua-card-header-color);
  padding-left: var(--ua-spacing-xl);
  text-decoration: none;
  width: 120px;
  text-align: center;
  display: inline-block;
}

.router-link--border {
  border-left: 2px solid rgba(255, 255, 255, 0.5);
}

.router-link.active,
.router-link:hover {
  font-weight: var(--ua-font-weight-semibold);
  color: rgb(var(--v-theme-accent));
  text-decoration: underline;
}
</style>
