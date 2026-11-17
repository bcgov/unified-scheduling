<script setup lang="ts">
import { computed, watch } from 'vue';
import { useRoute } from 'vue-router';
import { useTheme } from 'vuetify';
import Appbar from '@/shared/components/Appbar.vue';
import AppFooter from '@/shared/components/AppFooter.vue';
import UaBackgroundOverlay from '@/shared/components/UaBackgroundOverlay.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import { useBranding } from '@/composables/useBranding';
import { useNotificationsStore } from '@/stores/notifications';
import { useAuthStore } from '@/stores/auth';

const authStore = useAuthStore();
const route = useRoute();
const isLoggedOut = computed(() => route.name === 'LoggedOut');
const notifications = useNotificationsStore();
const isPrintMode = computed(() => route.path.startsWith('/calendar') && route.query.mode === 'print');
const { brandingStyles, backgroundImageUrl, primaryColor, backgroundColor, activeColor } = useBranding(isLoggedOut);
const theme = useTheme();

watch(
  [primaryColor, backgroundColor, activeColor],
  ([primary, background, active]) => {
    const colors = theme.themes.value.uaLight?.colors;
    if (!colors) return;

    colors.primary = primary;
    colors.background = background;
    colors.active = active;
  },
  { immediate: true },
);
</script>

<template>
  <v-app class="application-shell" :class="{ 'application-shell--logged-out': isLoggedOut }" :style="brandingStyles">
    <Appbar
      v-if="(authStore.isAuthenticated || isLoggedOut) && !route.meta.fullScreen && !isPrintMode"
      :public-view="isLoggedOut"
    />
    <UaBackgroundOverlay :image="isPrintMode ? null : backgroundImageUrl">
      <div
        class="application-layout"
        :class="{
          'application-layout--full-screen': route.meta.fullScreen,
          'application-layout--print': isPrintMode,
        }"
      >
        <main
          class="main-content"
          :class="{
            'main-content--full-screen': route.meta.fullScreen,
            'main-content--print': isPrintMode,
            'main-content--logged-out': isLoggedOut,
          }"
        >
          <RouterView v-if="!route.meta.requiresAuth || authStore.isAuthenticated" />
        </main>
        <AppFooter v-if="!isPrintMode" />
      </div>
    </UaBackgroundOverlay>
    <v-snackbar
      :model-value="notifications.visible"
      :color="notifications.color"
      :timeout="notifications.timeout"
      @update:model-value="!$event && notifications.dismiss()"
    >
      <span role="status" aria-live="polite" aria-atomic="true">{{ notifications.message }}</span>
      <template #actions>
        <UaBtn v-if="!notifications.pending" variant="text" @click="notifications.dismiss()">Dismiss</UaBtn>
      </template>
    </v-snackbar>
  </v-app>
</template>

<style scoped>
.application-shell {
  font-family: var(--ua-brand-font);
}
.application-shell--logged-out {
  --ua-appbar-height: 84px;
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
.application-layout--print {
  height: auto;
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

.main-content--print {
  height: auto;
  margin-top: 0;
  min-height: 0;
  overflow: visible;
  padding: 0;
}

.main-content--logged-out {
  display: flex;
  flex-direction: column;
  padding-bottom: var(--ua-spacing-md);
}

@media (max-width: 700px) {
  .application-shell--logged-out {
    --ua-appbar-height: 60px;
  }
}

@media print {
  :deep(.v-application__wrap) {
    min-height: 0;
  }
}
</style>
