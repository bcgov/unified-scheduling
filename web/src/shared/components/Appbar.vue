<script setup lang="ts">
import { useNavigationStore } from '@/stores/NavigationStore';
import { useAuthStore } from '@/stores/auth';
import { useLocationsStore } from '@/stores/LocationsStore';
import { mdiLogout } from '@mdi/js';

const bcgovLogo = new URL('/images/bcid-logo-rev-en.svg', import.meta.url).href;
const navigationStore = useNavigationStore();
const authStore = useAuthStore();
const locationsStore = useLocationsStore();

const handleLogout = () => {
  authStore.clearUserInfo();
  window.location.href = '/api/auth/logout';
};
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

      <v-spacer />

      <div v-if="authStore.isAuthenticated" class="appbar-actions">
        <select v-model="locationsStore.selectedLocationId" class="location-picker" aria-label="Select location">
          <option disabled :value="null">Select location</option>
          <option v-for="option in locationsStore.selectOptions" :key="option.code" :value="option.code">
            {{ option.description }}
          </option>
        </select>
        <v-menu min-width="200px" rounded>
          <template #activator="{ props }">
            <v-btn icon v-bind="props" class="avatar-btn">
              <v-avatar color="accent" size="36">
                <span class="initials-text">{{ authStore.initials }}</span>
              </v-avatar>
            </v-btn>
          </template>
          <v-card class="mt-2">
            <v-card-text>
              <div class="mx-auto text-center">
                <v-avatar color="accent" size="48" class="mb-2">
                  <span class="initials-text text-h6">{{ authStore.initials }}</span>
                </v-avatar>
                <h3>{{ authStore.userName }}</h3>
                <v-divider class="my-3"></v-divider>
                <v-btn variant="text" block @click="handleLogout" :prepend-icon="mdiLogout"> Logout </v-btn>
              </div>
            </v-card-text>
          </v-card>
        </v-menu>
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
  align-items: center;
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

.appbar-actions {
  display: flex;
  align-items: center;
  gap: var(--ua-spacing-md);
  margin-right: var(--ua-spacing-xl);
}

.location-picker {
  background-color: transparent;
  border: 1px solid rgba(255, 255, 255, 0.4);
  border-radius: 4px;
  color: #ffffff;
  padding: 6px 10px;
  font-size: 14px;
  cursor: pointer;
  min-width: 200px;
}

.location-picker:hover {
  border-color: rgba(255, 255, 255, 0.8);
}

.location-picker option {
  background-color: rgb(var(--v-theme-primary));
  color: #ffffff;
}

.avatar-btn {
  border: 1px solid rgba(255, 255, 255, 0.2);
}

.initials-text {
  color: rgb(var(--v-theme-primary));
  font-weight: var(--ua-font-weight-bold, 700);
}
</style>
