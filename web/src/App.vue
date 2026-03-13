<script setup lang="ts">
import Appbar from '@/shared/components/Appbar.vue';
</script>

<template>
  <v-app>
    <Appbar />
    <!-- Main Content -->
    <main class="main-content">
      <RouterView />
    </main>
  </v-app>
</template>

<style scoped>
.main-content {
  height: calc(100vh - 64px);
  margin-top: 64px;
  overflow-y: auto;
  padding: 0 8rem;
  background-color: rgba(var(--v-theme-surface-light), 0.6);
}
</style>
