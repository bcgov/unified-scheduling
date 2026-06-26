<script setup lang="ts">
import { useRoute } from 'vue-router';
import Appbar from '@/shared/components/Appbar.vue';

const route = useRoute();
</script>

<template>
  <v-app>
    <Appbar />
    <!-- Main Content -->
    <main class="main-content" :class="{ 'main-content--full-screen': route.meta.fullScreen }">
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

.main-content--full-screen {
  padding-inline: 0;
}
</style>
