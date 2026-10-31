<script setup lang="ts">
import { computed } from 'vue';
import { useNavigationStore } from '@/stores/NavigationStore';
import { useAuthStore } from '@/stores/auth';
import { useAccessControl } from '@/composables/useAccessControl';
import { useBranding } from '@/composables/useBranding';
import { getDashboardTiles, type DashboardTile } from '@/modules/dashboard/dashboardTiles.js';
import DefaultDashboardLayout from './template/defaultDashboardLayout.vue';
import DefaultDashboardBanner from './template/defaultDashboardBanner.vue';

const authStore = useAuthStore();
const accessControl = useAccessControl();
const branding = useBranding();
const navigationStore = useNavigationStore();

const greeting = computed(() => (authStore.userName ? `Hello, ${authStore.userName}` : 'Welcome'));

function availableDestinations(tiles: DashboardTile[]): DashboardTile[] {
  return tiles.flatMap((tile) => {
    if (tile.id === 'admin') {
      const destination = navigationStore.adminLinks[0];
      return destination ? [{ ...tile, path: destination.path }] : [];
    }
    if (!accessControl.featureFlags.value[tile.featureFlagKey]?.enabled) return [];
    if (tile.requiredPermission && !accessControl.hasPermission(tile.requiredPermission)) return [];
    if (
      tile.path.includes(':userId') &&
      (!authStore.currentUserId || !accessControl.featureFlags.value.UserManagement?.enabled)
    )
      return [];
    return [{ ...tile, path: tile.path.replace(':userId', authStore.currentUserId ?? '') }];
  });
}

const availableTiles = computed(() => availableDestinations(getDashboardTiles(branding.dashboardTiles.value)));
</script>

<template>
  <div class="dashboard">
    <DefaultDashboardBanner v-if="branding.dashboardBanner.value" :banner="branding.dashboardBanner.value" />
    <section class="dashboard-hero">
      <div class="dashboard-hero__text">
        <h1 class="dashboard-hero__title">{{ greeting }}</h1>
        <p class="dashboard-hero__guidance">
          {{ branding.dashboardMessage.value }}
        </p>
      </div>
    </section>

    <DefaultDashboardLayout :tiles="availableTiles" />
  </div>
</template>

<style scoped>
.dashboard {
  font-family: var(--ua-brand-font);
  color: var(--ua-brand-blue);
  display: flex;
  flex-direction: column;
  gap: 24px;
  padding-bottom: 24px;
}

.dashboard-hero {
  display: flex;
  align-items: center;
  background-color: transparent;
  padding: 0;
}

.dashboard-hero__title {
  margin: 0 0 4px;

  color: var(--ua-brand-blue);
  font-size: clamp(26px, 2.5vw, 34px);
  font-weight: 700;
  line-height: 1.2;
}

.dashboard-hero__guidance {
  margin: 0;
  color: var(--ua-brand-muted);
  font-size: clamp(17px, 1.7vw, 22px);

  line-height: 1.5;
}
</style>
