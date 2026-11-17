<script setup lang="ts">
import { useBranding } from '@/composables/useBranding';
import UaBtn from '@/shared/components/UaBtn.vue';
import { toApplicationReturnPath } from '@/utils/authNavigation';

const { applicationName } = useBranding();
const applicationRoot = toApplicationReturnPath('/');
</script>

<template>
  <section class="logged-out" aria-labelledby="logged-out-title">
    <p class="logged-out__application">{{ applicationName }}</p>
    <h1 id="logged-out-title" class="logged-out__title">You have been logged out</h1>
    <p class="logged-out__message">Your session has ended. Log in to continue.</p>
    <UaBtn color="primary" size="large" :href="applicationRoot">Log in</UaBtn>
  </section>
</template>

<style scoped>
.logged-out {
  flex: 1 0 auto;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: var(--ua-spacing-lg);
  padding-block: var(--ua-spacing-xl);
  font-family: var(--ua-brand-font);
  color: var(--ua-brand-blue);
  text-align: center;
  overflow-wrap: anywhere;
}

.logged-out__application {
  margin: 0;
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
}

.logged-out__title {
  margin: 0;
  font-size: clamp(26px, 2.5vw, 34px);
  font-weight: var(--ua-font-weight-bold);
  line-height: 1.2;
}

.logged-out__message {
  margin: 0;
  color: var(--ua-brand-muted);
  font-size: clamp(17px, 1.7vw, 22px);
  line-height: 1.5;
}
</style>
