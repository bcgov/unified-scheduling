<script setup lang="ts">
import UaCard from '@/shared/components/UaCard.vue';
import { mdiCalendarWeekOutline } from '@mdi/js';
import type { DashboardTile } from '@/modules/dashboard/dashboardTiles';

interface WeekScheduleDay {
  date: string;
  dayLabel: string;
  dateLabel: string;
  isToday: boolean;
  events: string[];
}

defineProps<{
  tiles: DashboardTile[];
  mySpaceTiles: DashboardTile[];
  myScheduleCard: DashboardTile;
  weekSchedule: WeekScheduleDay[];
  weekRangeLabel: string;
}>();
</script>

<template>
  <div class="dashboard-body">
    <div class="dashboard-body__main">
      <section class="dashboard-tiles-section">
        <h2 class="dashboard-tiles-section__title">Quick Links</h2>
        <section class="dashboard-tiles">
          <RouterLink v-for="tile in tiles" :key="tile.name" :to="tile.path" class="dashboard-tile-link">
            <UaCard class="dashboard-tile">
              <div class="dashboard-tile__icon">
                <v-icon :icon="tile.icon" size="32" />
              </div>
              <h3 class="dashboard-tile__title">{{ tile.name }}</h3>
              <p class="dashboard-tile__description">{{ tile.description }}</p>
            </UaCard>
          </RouterLink>
        </section>
      </section>

      <section class="dashboard-tiles-section">
        <h2 class="dashboard-tiles-section__title">My Space</h2>
        <section class="dashboard-tiles">
          <RouterLink
            v-for="tile in mySpaceTiles"
            :key="tile.name"
            :to="tile.path"
            class="dashboard-tile-link"
          >
            <UaCard class="dashboard-tile">
              <div class="dashboard-tile__icon">
                <v-icon :icon="tile.icon" size="32" />
              </div>
              <h3 class="dashboard-tile__title">{{ tile.name }}</h3>
              <p class="dashboard-tile__description">{{ tile.description }}</p>
            </UaCard>
          </RouterLink>
        </section>
      </section>
    </div>

    <div class="dashboard-body__divider" />

    <aside class="dashboard-body__sidebar">
      <h2 class="dashboard-tiles-section__title dashboard-week-schedule__title">
        <v-icon :icon="mdiCalendarWeekOutline" size="20" />
        My Schedule ({{ weekRangeLabel }})
      </h2>

      <div class="dashboard-week-schedule">
        <UaCard
          v-for="day in weekSchedule"
          :key="day.date"
          class="dashboard-week-schedule__card"
          :class="{ 'dashboard-week-schedule__card--today': day.isToday }"
        >
          <header class="dashboard-week-schedule__header">
            <span class="dashboard-week-schedule__day">{{ day.dayLabel }}</span>
            <span class="dashboard-week-schedule__day-number">{{ day.dateLabel }}</span>
          </header>
          <ul v-if="day.events.length" class="dashboard-week-schedule__events">
            <li v-for="event in day.events" :key="event" class="dashboard-week-schedule__event">
              {{ event }}
            </li>
          </ul>
          <p v-else class="dashboard-week-schedule__shift">No shifts scheduled</p>
        </UaCard>
      </div>

      <RouterLink :to="myScheduleCard.path" class="dashboard-week-schedule__view-all"> View full schedule </RouterLink>
    </aside>
  </div>
</template>

<style scoped>
.dashboard-body {
  display: grid;
  grid-template-columns: 1fr auto 20%;
  gap: var(--ua-spacing-xl);
  align-items: start;
}

.dashboard-body__main {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xl);
  min-width: 0;
}

.dashboard-body__divider {
  width: 1px;
  align-self: stretch;
  background-color: rgba(var(--v-theme-on-background), 0.15);
}

.dashboard-body__sidebar {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-md);
  min-width: 0;
}

.dashboard-week-schedule {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-sm);
}

.dashboard-week-schedule__card {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xs);
  padding: var(--ua-spacing-sm);
}

.dashboard-week-schedule__card--today {
  border: 1px solid rgb(var(--v-theme-primary));
}

.dashboard-week-schedule__header {
  display: flex;
  align-items: baseline;
  gap: var(--ua-spacing-xs);
  color: rgb(var(--v-theme-primary));
}

.dashboard-week-schedule__day {
  font-size: var(--ua-font-size-xs);
  text-transform: uppercase;
  font-weight: var(--ua-font-weight-semibold);
}

.dashboard-week-schedule__day-number {
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-bold);
}

.dashboard-week-schedule__events {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xs);
}

.dashboard-week-schedule__event {
  color: rgb(var(--v-theme-on-background));
  font-size: var(--ua-font-size-xs);
}

.dashboard-week-schedule__shift {
  color: var(--ua-text-muted);
  font-size: var(--ua-font-size-xs);
  margin: 0;
}

.dashboard-week-schedule__view-all {
  align-self: center;
  font-size: var(--ua-font-size-xs);
  font-weight: var(--ua-font-weight-semibold);
  color: rgb(var(--v-theme-primary));
  text-decoration: none;
}

.dashboard-week-schedule__view-all:hover {
  text-decoration: underline;
}

.dashboard-tiles-section__title {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  margin-bottom: var(--ua-spacing-md);
}

.dashboard-week-schedule__title {
  display: flex;
  align-items: center;
  gap: var(--ua-spacing-xs);
  color: rgb(var(--v-theme-primary));
}

.dashboard-tiles {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: var(--ua-spacing-lg);
}

.dashboard-tile-link {
  text-decoration: none;
  color: inherit;
}

.dashboard-tile {
  height: 100%;
  transition: box-shadow 0.15s ease-in-out;
}

.dashboard-tile-link:hover .dashboard-tile {
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.12);
}

.dashboard-tile__icon {
  color: rgb(var(--v-theme-primary));
  margin-bottom: var(--ua-spacing-sm);
}

.dashboard-tile__title {
  font-weight: var(--ua-font-weight-bold);
  margin-bottom: var(--ua-spacing-xs);
}

.dashboard-tile__description {
  color: var(--ua-text-muted);
  font-size: var(--ua-font-size-sm);
}
</style>
