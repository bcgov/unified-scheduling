<script setup lang="ts">
import { useRoute } from 'vue-router';
import Appbar from '@/shared/components/Appbar.vue';
import AppFooter from '@/shared/components/AppFooter.vue';
import UaBackgroundOverlay from '@/shared/components/UaBackgroundOverlay.vue';
import { useBranding } from '@/composables/useBranding';

const route = useRoute();
const { brandingStyles, backgroundImageUrl } = useBranding();
</script>

<template>
  <v-app class="application-shell" :style="brandingStyles">
    <Appbar v-if="!route.meta.fullScreen" />
    <UaBackgroundOverlay :image="backgroundImageUrl">
      <div class="application-layout" :class="{ 'application-layout--full-screen': route.meta.fullScreen }">
        <main class="main-content" :class="{ 'main-content--full-screen': route.meta.fullScreen }">
          <RouterView />
        </main>
        <AppFooter />
      </div>
    </UaBackgroundOverlay>
  </v-app>
</template>

<style scoped>
.application-shell {
  font-family: var(--ua-brand-font);
}
.application-layout {
  display: flex;
  flex-direction: column;
  height: 100vh;
  height: 100dvh;
  padding-top: var(--ua-appbar-height);
  box-sizing: border-box;
}
.application-layout--full-screen {
  padding-top: 0;
}
.main-content {
  flex: 1 1 auto;
  min-height: 0;
  overflow-y: auto;
  padding: 0 var(--ua-page-padding-x);
  background-color: transparent;
  padding-top: var(--ua-spacing-md);
}

.main-content--full-screen {
  padding-inline: 0;
  padding-top: 0;
}
</style>
