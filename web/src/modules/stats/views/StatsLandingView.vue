<script setup lang="ts">
const links = [
  {
    title: 'Non-Supervision Form',
    description: 'Enter non-supervision hours worked for a location and period.',
    to: { name: 'NonSupervisionForm' },
  },
  {
    title: 'Supervision Form',
    description: 'Enter supervision hours worked for a location and period.',
    to: { name: 'SupervisionForm' },
  },
  {
    title: 'Search / View / Edit Data',
    description: 'Search, view, and edit previously submitted stat records.',
    to: { name: 'StatSearch' },
  },
  {
    title: 'Monthly End Sign Offs',
    description: 'Review and sign off monthly stat records for a location.',
    to: { name: 'StatSignoffs' },
  },
];
</script>

<template>
  <div class="stats-landing-page">
    <h2 class="page-title">Stats</h2>

    <ul class="link-list">
      <li v-for="link in links" :key="link.title">
        <RouterLink :to="link.to" class="link-item">
          <span class="link-title">{{ link.title }}</span>
          <span class="link-desc">{{ link.description }}</span>
        </RouterLink>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.stats-landing-page {
  padding: var(--ua-spacing-xl);
  max-width: 520px;
}

.page-title {
  font-size: var(--ua-font-size-xl);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
  margin-bottom: var(--ua-spacing-lg);
}

.link-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xs);
}

.link-item {
  display: flex;
  flex-direction: column;
  padding: var(--ua-spacing-sm) 0;
  border-bottom: 1px solid var(--ua-border-color);
  text-decoration: none;
}

.link-item:hover .link-title {
  text-decoration: underline;
}

.link-title {
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
  color: var(--ua-link-color);
}

.link-desc {
  font-size: var(--ua-font-size-xs);
  color: var(--ua-text-secondary);
  margin-top: 0.15rem;
}
</style>
