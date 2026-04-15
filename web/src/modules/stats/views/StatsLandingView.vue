<script setup lang="ts">
import { mdiMagnify, mdiCheckAll, mdiClipboardTextOutline, mdiClipboardTextMultipleOutline } from '@mdi/js';

const links = [
  {
    title: 'Non-Supervision Form',
    description: 'Enter non-supervision hours worked for a location and period.',
    icon: mdiClipboardTextOutline,
    to: { name: 'NonSupervisionForm' },
  },
  {
    title: 'Supervision Form',
    description: 'Enter supervision hours worked for a location and period.',
    icon: mdiClipboardTextMultipleOutline,
    to: { name: 'SupervisionForm' },
  },
  {
    title: 'Search / View / Edit Data',
    description: 'Search, view, and edit previously submitted stat records.',
    icon: mdiMagnify,
    to: { name: 'StatSearch' },
  },
  {
    title: 'Monthly End Sign Offs',
    description: 'Review and sign off monthly stat records for a location.',
    icon: mdiCheckAll,
    to: { name: 'StatSignoffs' },
  },
];
</script>

<template>
  <div class="stats-landing-page">
    <div class="page-header">
      <h2 class="page-title">Stats</h2>
    </div>

    <div class="landing-grid">
      <v-card
        v-for="link in links"
        :key="link.title"
        class="landing-card"
        variant="outlined"
        :to="link.to"
        hover
      >
        <v-card-text class="landing-card-body">
          <v-icon :icon="link.icon" class="landing-icon" size="36" />
          <div>
            <div class="landing-card-title">{{ link.title }}</div>
            <div class="landing-card-desc">{{ link.description }}</div>
          </div>
        </v-card-text>
      </v-card>
    </div>
  </div>
</template>

<style scoped>
.stats-landing-page {
  padding: 2rem;
  max-width: 800px;
}

.page-header {
  margin-bottom: 2rem;
}

.page-title {
  font-size: 1.4rem;
  font-weight: 700;
  color: #1b2740;
}

.landing-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
  gap: 1.25rem;
}

.landing-card {
  border-radius: 10px;
  border-color: #d0d0d2;
  cursor: pointer;
  transition: box-shadow 0.15s ease;
  text-decoration: none;
}

.landing-card:hover {
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.landing-card-body {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
  padding: 1.4rem;
}

.landing-icon {
  color: #5f8f2c;
  flex-shrink: 0;
  margin-top: 2px;
}

.landing-card-title {
  font-size: 1rem;
  font-weight: 700;
  color: #1b2740;
  margin-bottom: 0.3rem;
}

.landing-card-desc {
  font-size: 0.875rem;
  color: #5a5a5a;
  line-height: 1.45;
}
</style>
