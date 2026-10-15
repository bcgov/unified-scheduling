<script setup lang="ts">
import { computed } from 'vue';
import { DateTime } from 'luxon';
import { useAuthStore } from '@/stores/auth';
import { useAccessControl } from '@/composables/useAccessControl';
import { useBranding } from '@/composables/useBranding';
import { dashboardTiles, mySpaceTiles, myScheduleCard } from '@/modules/dashboard/dashboardTiles.js';
import DefaultDashboardLayout from './template/defaultDashboardLayout.vue';

const authStore = useAuthStore();
const accessControl = useAccessControl();
const branding = useBranding();

const greeting = computed(() => (authStore.userName ? `Hello, ${authStore.userName}` : 'Welcome'));

const availableTiles = computed(() =>
  dashboardTiles.filter((tile) => accessControl.featureFlags.value[tile.featureFlagKey]?.enabled),
);

const availableMySpaceTiles = computed(() =>
  mySpaceTiles
    .filter((tile) => accessControl.featureFlags.value[tile.featureFlagKey]?.enabled)
    .map((tile) => ({
      ...tile,
      path: tile.path.replace(':userId', authStore.currentUserId ?? ''),
    })),
);

// Placeholder week-at-a-glance data (Mon-Sun) until the schedule is wired to
// the Calendar/Scheduling API for the signed-in user.
const weekSchedule = computed(() => {
  const startOfWeek = DateTime.now().startOf('week');
  return Array.from({ length: 7 }, (_, i) => {
    const date = startOfWeek.plus({ days: i });
    return {
      date: date.toISODate() ?? '',
      dayLabel: date.toFormat('ccc'),
      dateLabel: date.toFormat('LLL d'),
      isToday: date.hasSame(DateTime.now(), 'day'),
      events: [] as string[],
    };
  });
});

const weekRangeLabel = computed(() => {
  const startOfWeek = DateTime.now().startOf('week');
  const endOfWeek = startOfWeek.endOf('week');
  const sameMonth = startOfWeek.hasSame(endOfWeek, 'month');
  const start = startOfWeek.toFormat(sameMonth ? 'LLL d' : 'LLL d');
  const end = endOfWeek.toFormat(sameMonth ? 'd' : 'LLL d');
  return `${start} – ${end}`;
});
</script>

<template>
  <div class="dashboard">
    <section class="dashboard-hero">
      <div class="dashboard-hero__text">
        <h1 class="dashboard-hero__title">Welcome to the {{ branding.applicationName.value }}</h1>
        <p class="dashboard-hero__subtitle">{{ greeting }}</p>
        <p class="dashboard-hero__guidance">
          Use the navigation bar above to jump into a module, or select one of the tiles below to get started.
        </p>
      </div>
    </section>
    <div class="dashboard-hero__accent" />

    <DefaultDashboardLayout
      :tiles="availableTiles"
      :my-space-tiles="availableMySpaceTiles"
      :my-schedule-card="myScheduleCard"
      :week-schedule="weekSchedule"
      :week-range-label="weekRangeLabel"
    />
  </div>
</template>

<style scoped>
.dashboard {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xl);
}

.dashboard-hero {
  display: flex;
  align-items: center;
  gap: var(--ua-spacing-xl);
  background-color: transparent;
  color: rgb(var(--v-theme-on-background));
  padding: var(--ua-spacing-xl);
  padding-bottom: 0;
  border-radius: var(--ua-card-border-radius);
}

.dashboard-hero__title {
  font-size: var(--ua-font-size-xl);
  font-weight: var(--ua-font-weight-bold);
  margin-bottom: 0;
}

.dashboard-hero__subtitle {
  font-weight: var(--ua-font-weight-semibold);
  color: rgb(var(--v-theme-primary));
  margin-bottom: 0;
}

.dashboard-hero__guidance {
  color: var(--ua-text-muted);
}

.dashboard-hero__accent {
  height: 4px;
  background-color: rgb(var(--v-theme-primary));
  border-radius: var(--ua-card-border-radius);
  margin-top: calc(var(--ua-spacing-xl) * -1 + var(--ua-spacing-xs));
}
</style>
