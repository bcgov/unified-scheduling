<script setup lang="ts">
const logoUrl = new URL(`${import.meta.env.BASE_URL}images/bcid-logo-rev-en.svg`, window.location.origin).toString();
</script>

<template>
  <div class="unavailable-page">
    <header class="unavailable-header">
      <img width="177" height="44" :src="logoUrl" alt="B.C. Government Logo" />
    </header>
    <div class="gold-accent-bar" />

    <main class="unavailable-content" aria-labelledby="unavailable-title">
      <section class="unavailable-panel" role="alert">
        <h1 id="unavailable-title">Application unavailable</h1>
        <p>
          The Unified Scheduling application is currently unavailable. Please refresh your application. If you
          continue to see this error, contact support.
        </p>
      </section>
    </main>
  </div>
</template>

<style scoped>
.unavailable-page {
  min-height: 100vh;
  color: var(--bcds-typography-color-primary);
  background-color: var(--bcds-surface-color-background-light-gray);
}

.unavailable-header {
  display: flex;
  align-items: center;
  min-height: 64px;
  padding: var(--ua-spacing-sm) var(--ua-spacing-xl);
  background-color: var(--bcds-theme-primary-blue);
}

.unavailable-header img {
  margin-left: clamp(var(--ua-spacing-xs), 1.2vw, var(--ua-spacing-xl));
}

.gold-accent-bar {
  height: 4px;
  background-color: var(--bcds-theme-primary-gold);
}

.unavailable-content {
  display: flex;
  justify-content: center;
  padding: var(--ua-spacing-xl) var(--ua-page-padding-x);
}

.unavailable-panel {
  width: min(100%, 720px);
  padding: var(--ua-spacing-xl);
  border-left: 4px solid var(--bcds-support-border-color-danger);
  border-radius: 4px;
  background-color: var(--bcds-surface-color-background-white);
  box-shadow: 0 2px 8px rgb(0 0 0 / 12%);
}

.unavailable-panel h1 {
  margin: 0 0 var(--ua-spacing-md);
  color: var(--bcds-theme-primary-blue);
  font-size: 1.75rem;
}

.unavailable-panel p {
  margin: 0;
  line-height: 1.5;
}
</style>
