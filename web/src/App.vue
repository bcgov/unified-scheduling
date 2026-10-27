<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import Appbar from '@/shared/components/Appbar.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import { useNotificationsStore } from '@/stores/notifications';

const route = useRoute();
const notifications = useNotificationsStore();
const isPrintMode = computed(() => route.path.startsWith('/calendar') && route.query.mode === 'print');
</script>

<template>
  <v-app>
    <Appbar v-if="!route.meta.fullScreen && !isPrintMode" />
    <!-- Main Content -->
    <main
      class="main-content"
      :class="{
        'main-content--full-screen': route.meta.fullScreen,
        'main-content--print': isPrintMode,
      }"
    >
      <RouterView />
    </main>
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
.main-content {
  height: calc(100vh - var(--ua-appbar-height));
  margin-top: var(--ua-appbar-height);
  overflow-y: auto;
  padding: 0 var(--ua-page-padding-x);
  background-color: rgb(var(--v-theme-background));
  padding-top: var(--ua-spacing-md);
}

.main-content--full-screen {
  height: 100vh;
  margin-top: 0;
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

@media print {
  :deep(.v-application__wrap) {
    min-height: 0;
  }
}
</style>
