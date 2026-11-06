<script setup lang="ts">
import UaCard from '@/shared/components/UaCard.vue';
import { mdiChevronRight } from '@mdi/js';
import type { DashboardTile } from '@/modules/dashboard/dashboardTiles';

defineProps<{ tiles: DashboardTile[] }>();
</script>

<template>
  <div class="dashboard-layout">
    <section class="dashboard-tiles" aria-label="Modules">
      <RouterLink v-for="tile in tiles" :key="tile.id" :to="tile.path" class="dashboard-tile-link">
        <UaCard class="dashboard-tile" :data-tile="tile.name" body-padding="none">
          <div class="dashboard-tile__content">
            <div class="dashboard-tile__heading">
              <div class="dashboard-tile__icon">
                <v-icon :icon="tile.icon" size="42" />
              </div>
            </div>
            <h3 class="dashboard-tile__title">{{ tile.name }}</h3>
            <p class="dashboard-tile__description">{{ tile.description }}</p>

            <div class="dashboard-tile__footer" aria-hidden="true">
              <span class="dashboard-tile__arrow"><v-icon :icon="mdiChevronRight" size="26" /></span>
            </div>
          </div>
        </UaCard>
      </RouterLink>
    </section>
  </div>
</template>

<style scoped>
.dashboard-layout {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.dashboard-tiles {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(100%, 320px), 1fr));
  gap: 14px;
  align-items: stretch;
}

.dashboard-tile-link {
  display: flex;
  width: 100%;
  max-width: 400px;
  min-width: 0;
  text-decoration: none;
  color: inherit;
  border-radius: 10px;
}

.dashboard-tile {
  --tile-accent: var(--ua-brand-blue);
  --tile-icon-background: #e3e8ef;
  --tile-icon-color: var(--ua-brand-blue);

  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
  height: 320px;
  max-width: 100%;
  overflow: hidden;
  background: #fff;
  border: 1px solid #e6ecf3;
  border-top: 5px solid var(--tile-accent);
  border-radius: 10px;
  box-shadow: 0 10px 26px rgba(25, 49, 83, 0.06);
  transition:
    transform 0.15s ease-in-out,
    box-shadow 0.15s ease-in-out;
}

/* UaCard wraps its default slot in a body element. Stretch that wrapper
   so every card's footer aligns at the bottom of its grid row. */
.dashboard-tile :deep(.ua-card__body) {
  display: flex;
  flex: 1;
  min-width: 0;
  min-height: 0;
  padding: 0;
}

.dashboard-tile[data-tile='My Team'] {
  --tile-accent: var(--ua-brand-blue);
  --tile-icon-background: #dfeaf8;
  --tile-icon-color: var(--ua-brand-blue);
}

.dashboard-tile[data-tile='Stats'] {
  --tile-accent: var(--ua-brand-blue);
  --tile-icon-background: #e3e8ef;
  --tile-icon-color: var(--ua-brand-blue);
}

.dashboard-tile[data-tile='Schedule'] {
  --tile-accent: var(--ua-brand-muted);
  --tile-icon-background: #edf2f8;
  --tile-icon-color: var(--ua-brand-blue);
}

.dashboard-tile[data-tile='Admin'] {
  --tile-accent: var(--ua-brand-gold);
  --tile-icon-background: #fce9b6;
  --tile-icon-color: #8a5f14;
}

.dashboard-tile-link:hover .dashboard-tile {
  transform: translateY(-2px);
  box-shadow: 0 12px 28px rgba(25, 49, 83, 0.12);
}

.dashboard-tile-link:focus-visible {
  outline: 3px solid #728bb5;
  outline-offset: 4px;
}

.dashboard-tile__content {
  flex: 1;
  min-width: 0;
  display: grid;
  grid-template-columns: 72px minmax(0, 1fr);
  grid-template-rows: auto minmax(0, 1fr) auto;
  column-gap: 20px;
  min-height: 0;
  width: 100%;
  height: 100%;
  box-sizing: border-box;
  padding: 20px 24px 16px;
}

.dashboard-tile__heading {
  grid-column: 1;
  grid-row: 1 / 3;
}

.dashboard-tile__icon {
  display: flex;
  width: 72px;
  height: 72px;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  color: var(--tile-icon-color);
  background: var(--tile-icon-background);
}

.dashboard-tile__title {
  grid-column: 2;
  margin: 12px 0 10px;
  color: var(--ua-brand-blue);
  font-size: 28px;
  line-height: 1.2;
  font-weight: 700;
  overflow-wrap: anywhere;
}

.dashboard-tile__description {
  grid-column: 2;
  min-height: 0;
  overflow-y: auto;
  margin: 0;
  color: var(--ua-brand-muted);
  font-size: 16px;
  line-height: 1.35;
  overflow-wrap: anywhere;
}

.dashboard-tile__footer {
  grid-column: 1 / -1;
  display: flex;
  justify-content: flex-end;
  align-items: center;
  margin-top: 12px;
  padding-top: 8px;
  border-top: 2px solid #e8eef6;
}

.dashboard-tile__arrow {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 48px;
  height: 48px;
  border-radius: 50%;
  background: #eaf0f7;
  color: var(--ua-brand-blue);
}

@media (max-width: 600px) {
  .dashboard-tile__content {
    grid-template-columns: 64px minmax(0, 1fr);
    column-gap: 18px;
    padding: 20px 18px 16px;
  }

  .dashboard-tile__icon {
    width: 64px;
    height: 64px;
  }

  .dashboard-tile__title {
    margin-top: 12px;
    font-size: 24px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .dashboard-tile {
    transition: none;
  }

  .dashboard-tile-link:hover .dashboard-tile {
    transform: none;
  }
}
</style>
