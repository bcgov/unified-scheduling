<script setup lang="ts">
import { useRoute } from 'vue-router';
import Appbar from '@/shared/components/Appbar.vue';
import UaBackgroundOverlay from '@/shared/components/UaBackgroundOverlay.vue';
import { useBranding } from '@/composables/useBranding';

const route = useRoute();
const branding = useBranding();
</script>

<template>
  <v-app>
    <Appbar v-if="!route.meta.fullScreen" />
    <UaBackgroundOverlay :image="branding.backgroundImageUrl.value">
      <!-- Main Content -->
      <main class="main-content" :class="{ 'main-content--full-screen': route.meta.fullScreen }">
        <RouterView />
      </main>
    </UaBackgroundOverlay>
  </v-app>
</template>

<style scoped>
.main-content {
  height: calc(100vh - var(--ua-appbar-height));
  margin-top: var(--ua-appbar-height);
  overflow-y: auto;
  padding: 0 var(--ua-page-padding-x);
  background-color: transparent;
  padding-top: var(--ua-spacing-md);
}

.main-content--full-screen {
  height: 100vh;
  margin-top: 0;
  padding-inline: 0;
  padding-top: 0;
}
</style>
