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
  padding: 2rem;
  max-width: 520px;
}

.page-title {
  font-size: 1.4rem;
  font-weight: 700;
  color: #1b2740;
  margin-bottom: 1.5rem;
}

.link-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.link-item {
  display: flex;
  flex-direction: column;
  padding: 0.6rem 0;
  border-bottom: 1px solid #e0e0e0;
  text-decoration: none;
}

.link-item:hover .link-title {
  text-decoration: underline;
}

.link-title {
  font-size: 0.95rem;
  font-weight: 600;
  color: #1a5296;
}

.link-desc {
  font-size: 0.825rem;
  color: #5a5a5a;
  margin-top: 0.15rem;
}
</style>
