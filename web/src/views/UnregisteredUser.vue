<script setup lang="ts">
import { computed } from 'vue';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaCard from '@/shared/components/UaCard.vue';
import { useConfigStore } from '@/stores/config';

const configStore = useConfigStore();

const supportEmail = computed(() => configStore.supportEmail ?? 'support');
const applicationName = computed(() => configStore.applicationName ?? 'Unified Scheduling');
</script>

<template>
  <section class="unregistered-page">
    <UaCard class="unregistered-page__card">
      <template #header>
        <span class="unregistered-page__title">Welcome to the {{ applicationName }} application.</span>
      </template>
      <UaAlert type="warning" :closable="false" class="unregistered-page__alert">
        You are not currently registered to use this application.
      </UaAlert>
      <p class="unregistered-page__message">
        To register, please email the application support contact below, and request access to the application by
        providing your IDIR:
      </p>
      <a class="unregistered-page__email-link" :href="`mailto:${supportEmail}`">{{ supportEmail }}</a>
    </UaCard>
  </section>
</template>

<style scoped>
.unregistered-page {
  min-height: 100%;
  display: grid;
  place-items: center;
  padding: 0 var(--ua-spacing-2xl) var(--ua-spacing-2xl);
  background-color: rgb(var(--v-theme-background));
}

.unregistered-page__card {
  width: min(100%, 48rem);
}

.unregistered-page__title {
  display: block;
  width: 100%;
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  text-align: center;
}

.unregistered-page__alert {
  margin-bottom: var(--ua-spacing-xl);
}

.unregistered-page__message {
  font-size: var(--ua-font-size-lg);
  line-height: 1.6;
  color: var(--ua-text-primary);
  text-align: center;
  margin: var(--ua-spacing-2xl) 0;
}

.unregistered-page__email-link {
  display: block;
  width: fit-content;
  margin: 0 auto;
  color: rgb(var(--v-theme-primary));
}
</style>
