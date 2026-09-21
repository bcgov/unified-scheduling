<script setup lang="ts">
defineProps<{
  canCancel: boolean;
  isSeries: boolean;
}>();
</script>

<template>
  <section class="assignment-lifecycle-panel" aria-label="Assignment Lifecycle Panel">
    <h3 class="assignment-lifecycle-panel__heading">
      {{ canCancel ? 'Cancel' : 'Delete' }}
      {{ isSeries ? 'assignment series' : 'assignment' }}
    </h3>
    <p>
      {{
        canCancel
          ? 'Cancelling this published assignment will remove it from active scheduling views.'
          : 'Deleting this draft assignment is permanent and cannot be undone.'
      }}
    </p>
  </section>
</template>

<style scoped>
.assignment-lifecycle-panel {
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  display: grid;
  gap: var(--ua-spacing-sm);
  padding: var(--ua-spacing-lg);
}

.assignment-lifecycle-panel__heading {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-bold);
  margin: 0;
}

.assignment-lifecycle-panel p {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
}
</style>
