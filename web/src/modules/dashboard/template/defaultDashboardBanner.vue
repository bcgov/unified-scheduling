<script setup lang="ts">
import type { DashboardBanner } from '@/branding/types';

defineProps<{ banner: DashboardBanner }>();
</script>

<template>
  <section
    class="dashboard-banner"
    :class="{ 'dashboard-banner--with-image': banner.imageUrl }"
    :aria-label="banner.title"
  >
    <img v-if="banner.imageUrl" class="dashboard-banner__image" :src="banner.imageUrl" alt="" />
    <template v-if="banner.logoUrl">
      <img class="dashboard-banner__logo" :src="banner.logoUrl" :alt="banner.logoAlt ?? ''" />
      <div class="dashboard-banner__divider" aria-hidden="true" />
    </template>
    <div class="dashboard-banner__text">
      <h2 class="dashboard-banner__title">{{ banner.title }}</h2>
      <p v-if="banner.subtitle" class="dashboard-banner__subtitle">{{ banner.subtitle }}</p>
      <div v-if="banner.tagline" class="dashboard-banner__accent" />
      <p v-if="banner.tagline" class="dashboard-banner__tagline">{{ banner.tagline }}</p>
    </div>
  </section>
</template>

<style scoped>
.dashboard-banner {
  position: relative;
  isolation: isolate;
  overflow: hidden;
  min-height: 280px;
  display: flex;
  align-items: center;
  gap: clamp(20px, 2.4vw, 40px);
  margin: calc(var(--ua-spacing-md) * -1) calc(var(--ua-page-padding-x) * -1) 0;
  padding: 40px var(--ua-page-padding-x);
  background: linear-gradient(
    110deg,
    color-mix(in srgb, var(--ua-brand-blue) 4%, white) 35%,
    color-mix(in srgb, var(--ua-brand-blue) 12%, white) 65%,
    color-mix(in srgb, var(--ua-brand-blue) 22%, white)
  );
}

.dashboard-banner__text {
  min-width: 0;
}

.dashboard-banner__logo {
  width: clamp(180px, 16vw, 255px);
  max-height: 320px;
  flex: 0 0 auto;
  object-fit: contain;
}

.dashboard-banner__divider {
  width: 2px;
  height: 240px;
  flex: 0 0 auto;
  background: #ced8e8;
}

.dashboard-banner__image {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
  z-index: -2;
}

.dashboard-banner--with-image::before {
  content: '';
  position: absolute;
  inset: 0;
  z-index: -1;
  background: linear-gradient(90deg, #fff 30%, rgba(255, 255, 255, 0.9) 45%, transparent 85%);
}

.dashboard-banner__title {
  margin: 0;
  color: var(--ua-brand-blue);
  font-size: clamp(32px, 4.8vw, 80px);
  font-weight: 700;
  line-height: 1.1;
  overflow-wrap: anywhere;
}

.dashboard-banner__subtitle {
  margin: 4px 0 0;
  color: var(--ua-brand-blue);
  font-size: clamp(20px, 2.2vw, 34px);
  line-height: 1.3;
  text-transform: uppercase;
}

.dashboard-banner__accent {
  width: 74px;
  height: 4px;
  margin-block: 14px;
  background: var(--ua-brand-gold);
}

.dashboard-banner__tagline {
  margin: 0;
  color: var(--ua-brand-muted);
  font-size: clamp(16px, 1.8vw, 23px);
  line-height: 1.4;
}

@media (max-width: 600px) {
  .dashboard-banner {
    flex-direction: column;
    align-items: flex-start;
    gap: 20px;
  }

  .dashboard-banner__logo {
    width: clamp(170px, 52vw, 220px);
    height: 280px;
  }

  .dashboard-banner__divider {
    display: none;
  }
}
</style>
