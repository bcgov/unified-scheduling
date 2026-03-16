<script setup lang="ts">
import { useNavigationStore } from '@/stores/NavigationStore';

const navigationStore = useNavigationStore();
</script>

<template>
  <v-app-bar class="app-bar" density="compact">
    <div style="margin-left: 2rem">
      <img width="132" src="../../assets/images/bcid-logo-en.svg" alt="" />
    </div>
    <div class="router-link-container">
      <div v-for="navItem in navigationStore.links" :key="navItem.name">
        <RouterLink :class="['router-link', navItem?.class ?? '']" :to="navItem.path" active-class="active">
          {{ navItem.name }}
        </RouterLink>
      </div>
    </div>
  </v-app-bar>
</template>

<style>
.app-bar {
  background-color: #eaeaea;
  padding: 0.5rem 2rem;
}

.router-link-container {
  display: flex;
  gap: 2rem;
  margin-left: 8rem;
}

.router-link {
  color: rgba(var(--v-theme-on-surface), 0.87);
  padding-left: 2rem;
  text-decoration: none;
}

.router-link--border {
  border-left: 2px solid #333;
}

.router-link.active,
.router-link:hover {
  font-weight: 500;
  color: rgba(var(--v-theme-primary), 1);
  text-decoration: underline;
}
</style>
