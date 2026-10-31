<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import { useNavigationStore } from '@/stores/NavigationStore';
import { useAuthStore } from '@/stores/auth';
import { useLocationsStore } from '@/stores/LocationsStore';
import { mdiLogout, mdiMapMarker, mdiChevronDown } from '@mdi/js';
import UaSelect from '@/shared/components/UaSelect.vue';
import { redirectToLogout } from '@/utils/authNavigation';
import { useBranding } from '@/composables/useBranding';

const navigationStore = useNavigationStore();
const authStore = useAuthStore();
const locationsStore = useLocationsStore();
const branding = useBranding();
const route = useRoute();
const isAdminActive = computed(() =>
  navigationStore.adminLinks.some((link) => route.path === link.path || route.path.startsWith(`${link.path}/`)),
);

const handleLogout = () => {
  authStore.clearUserInfo();
  redirectToLogout();
};
</script>

<template>
  <div class="app-bar-wrapper">
    <v-app-bar class="app-bar" :height="80" flat>
      <RouterLink class="appbar-branding appbar-branding-link" to="/dashboard" aria-label="Go to dashboard home">
        <div class="appbar-logo">
          <img :src="branding.primaryLogoUrl.value" :alt="branding.primaryLogoAlt.value" />
        </div>
        <template v-if="branding.secondaryLogoUrl.value">
          <div class="appbar-logo-separator" />
          <div class="appbar-logo appbar-logo--secondary">
            <img :src="branding.secondaryLogoUrl.value" :alt="branding.secondaryLogoAlt.value" />
          </div>
        </template>
      </RouterLink>
      <div v-if="authStore.isRegistered" class="router-link-container">
        <RouterLink
          v-for="navItem in navigationStore.mainLinks"
          :key="navItem.name"
          class="router-link"
          :to="navItem.path"
          active-class="active"
        >
          {{ navItem.name }}
        </RouterLink>
        <v-menu v-if="navigationStore.adminLinks.length">
          <template #activator="{ props }">
            <button v-bind="props" type="button" class="router-link" :class="{ active: isAdminActive }">
              Admin <v-icon :icon="mdiChevronDown" size="18" />
            </button>
          </template>
          <v-list aria-label="Administration">
            <v-list-item
              v-for="link in navigationStore.adminLinks"
              :key="link.path"
              :to="link.path"
              :title="link.name"
            />
          </v-list>
        </v-menu>
      </div>

      <v-spacer />

      <div v-if="authStore.isAuthenticated" class="appbar-actions">
        <UaSelect
          v-if="authStore.isRegistered"
          v-model="locationsStore.selectedLocationId"
          :items="locationsStore.selectOptions"
          label="Select location"
          :prepend-inner-icon="mdiMapMarker"
          :menu-icon="mdiChevronDown"
          item-color="#000000"
          density="comfortable"
          variant="outlined"
          hide-details
          class="location-picker"
        />
        <v-menu min-width="200px" rounded>
          <template #activator="{ props }">
            <v-btn icon v-bind="props" class="avatar-btn">
              <v-avatar color="accent" size="40">
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

<style scoped>
.app-bar-wrapper {
  font-family: var(--ua-brand-font);
  position: fixed;
  inset: 0 0 auto;
  z-index: 1000;
}

.app-bar {
  position: static !important;
  padding-inline: clamp(16px, 2.7vw, 44px);
  background: var(--ua-brand-blue) !important;
}

.gold-accent-bar {
  height: 4px;
  background: var(--ua-brand-gold);
}
.appbar-branding {
  display: flex;
  align-items: center;
  gap: 24px;
  flex: 0 0 auto;
}
.appbar-branding-link {
  color: inherit;
  text-decoration: none;
}
.appbar-branding-link:focus-visible {
  outline: 2px solid var(--ua-navigation-selected);
  outline-offset: 3px;
}

.appbar-logo {
  display: flex;
  align-items: center;
  flex: 0 0 auto;
  height: 76px;
}

.appbar-logo img {
  display: block;
  width: auto;
  height: 48px;
  max-width: 192px;
  object-fit: contain;
}
.appbar-logo--secondary img {
  height: 76px;
  max-width: 220px;
}

.appbar-logo-separator {
  width: 2px;
  height: 56px;
  background: rgba(255, 255, 255, 0.4);
}

.router-link-container {
  display: flex;
  align-self: stretch;
  align-items: stretch;
  flex: 1 1 auto;
  min-width: 0;
  margin-left: clamp(16px, 2.4vw, 40px);
  overflow-x: auto;
}

.router-link {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  flex: 0 0 auto;
  padding: 0 clamp(16px, 1.8vw, 30px);
  border: 0;
  background: transparent;
  color: #fff;
  font: inherit;
  font-size: 18px;
  line-height: 1.25;
  text-decoration: none;
  white-space: nowrap;
  cursor: pointer;
}

.router-link + .router-link::before {
  content: '';
  position: absolute;
  left: 0;
  top: 50%;
  height: 30px;
  width: 2px;
  transform: translateY(-50%);
  background: rgba(255, 255, 255, 0.4);
}

.router-link.active {
  color: var(--ua-navigation-selected);
  font-weight: 700;
}
.router-link.active:hover {
  color: var(--ua-navigation-selected);
}
.router-link.active::after {
  content: '';
  position: absolute;
  bottom: 0;
  left: 12px;
  right: 12px;
  height: 4px;
  background: var(--ua-navigation-selected);
}
.router-link:hover {
  color: #fff;
  background: rgba(255, 255, 255, 0.06);
}
.router-link:focus-visible {
  outline: 2px solid var(--ua-navigation-selected);
  outline-offset: -4px;
}

.appbar-actions {
  display: flex;
  align-items: center;
  gap: 20px;
  flex: 0 0 auto;
  margin-left: 24px;
}

.location-picker {
  width: clamp(320px, 25vw, 440px);
  flex: 0 0 auto;
  background: #fff;
  border-radius: 12px;
  color: #000;
}

.location-picker :deep(.v-field) {
  border-radius: 12px;
}
.location-picker :deep(.v-field__input) {
  min-height: 50px;
  padding-block: 7px;
  font-size: 16px;
  font-weight: 700;
}
.location-picker :deep(.v-select__selection) {
  min-width: 0;
  max-width: 100%;
}
.location-picker :deep(.v-select__selection-text) {
  color: #000;
  white-space: normal;
  overflow-wrap: anywhere;
  line-height: 1.3;
}

.avatar-btn {
  width: 40px;
  height: 40px;
}

.initials-text {
  color: var(--ua-brand-blue);
  font-weight: 700;
}
@media (max-width: 1200px) {
  .appbar-branding {
    gap: 12px;
  }
  .appbar-logo img {
    height: 40px;
    max-width: 160px;
  }
  .appbar-logo--secondary img {
    height: 76px;
    max-width: 220px;
  }
  .router-link-container {
    margin-left: 16px;
  }
  .router-link {
    padding-inline: 16px;
    font-size: 16px;
  }
  .appbar-actions {
    gap: 12px;
    margin-left: 12px;
  }
}
@media (max-width: 700px) {
  .app-bar :deep(.v-toolbar__content) {
    overflow-x: auto;
  }
  .router-link-container {
    flex-shrink: 0;
  }
}
</style>
