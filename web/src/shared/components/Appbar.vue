<script setup lang="ts">
import { useNavigationStore } from '@/stores/NavigationStore';
import { useAuthStore } from '@/stores/auth';
import { useLocationsStore } from '@/stores/LocationsStore';
import { mdiLogout, mdiEarth } from '@mdi/js';
import UaSelect from '@/shared/components/UaSelect.vue';

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
      <div class="appbar-logo">
        <img width="177" height="44" :src="bcgovLogo" alt="B.C. Government Logo" />
      </div>
      <div v-if="authStore.isRegistered" class="router-link-container">
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
        <UaSelect
          v-if="authStore.isRegistered"
          v-model="locationsStore.selectedLocationId"
          :items="locationsStore.selectOptions"
          label="Select location"
          :prepend-inner-icon="mdiEarth"
          class="location-picker"
        />
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

.appbar-logo {
  margin-left: clamp(var(--ua-spacing-xs), 1.2vw, var(--ua-spacing-xl));
  margin-right: clamp(var(--ua-spacing-xs), 0.8vw, var(--ua-spacing-md));
  flex: 0 0 auto;
}

.gold-accent-bar {
  height: 4px;
  background-color: rgb(var(--v-theme-accent));
}

.router-link-container {
  display: flex;
  gap: clamp(var(--ua-spacing-xs), 0.8vw, var(--ua-spacing-md));
  margin-left: clamp(var(--ua-spacing-xs), 1.2vw, var(--ua-spacing-xl));
  align-items: center;
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
}

.router-link {
  color: var(--ua-card-header-color);
  padding-left: clamp(var(--ua-spacing-xs), 0.6vw, var(--ua-spacing-md));
  text-decoration: none;
  min-width: clamp(72px, 5.5vw, 92px);
  width: auto;
  text-align: center;
  display: inline-block;
  white-space: nowrap;
  font-size: clamp(0.86rem, 0.82rem + 0.2vw, 1rem);
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
  gap: clamp(var(--ua-spacing-xs), 0.8vw, var(--ua-spacing-md));
  margin-right: clamp(var(--ua-spacing-xs), 1.2vw, var(--ua-spacing-xl));
  flex: 0 0 auto;
  min-width: 0;
}

.location-picker {
  width: clamp(160px, 20vw, 320px);
  min-width: 0;
  font-size: clamp(0.9rem, 0.85rem + 0.15vw, 1rem);
  background: var(--ua-field-bg);
}

.location-picker :deep(.v-field__input) {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.avatar-btn {
  border: 1px solid rgba(255, 255, 255, 0.2);
}

.initials-text {
  color: rgb(var(--v-theme-primary));
  font-weight: var(--ua-font-weight-bold, 700);
}
</style>
