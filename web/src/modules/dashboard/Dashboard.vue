<script setup lang="ts">
import { computed } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useBranding } from '@/composables/useBranding';
import { getDashboardTiles } from '@/modules/dashboard/dashboardTiles.js';
import DefaultDashboardLayout from './template/defaultDashboardLayout.vue';
import DefaultDashboardBanner from './template/defaultDashboardBanner.vue';

const authStore = useAuthStore();
const branding = useBranding();

const greeting = computed(() => (authStore.userName ? `Hello, ${authStore.userName}` : 'Welcome'));
const availableTiles = computed(() => getDashboardTiles(branding.dashboardTiles.value));
</script>

<template>
  <div class="dashboard">
    <DefaultDashboardBanner v-if="branding.dashboardBanner.value" :banner="branding.dashboardBanner.value" />
    <section class="dashboard-hero">
      <div class="dashboard-hero__text">
        <p class="dashboard-hero__title">{{ greeting }}</p>
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
