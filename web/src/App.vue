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
  height: calc(100vh - var(--ua-appbar-height));
  margin-top: var(--ua-appbar-height);
  overflow-y: auto;
  padding: 0 var(--ua-page-padding-x);
  background-color: rgb(var(--v-theme-background));
  padding-top: var(--ua-spacing-md);
}
</style>
