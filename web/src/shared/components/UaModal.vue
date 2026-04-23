<script setup lang="ts">
const { title, persistent, width, loading } = withDefaults(
  defineProps<{
    /** Title displayed in the modal header. */
    title: string;
    /** If true, clicking outside does not close the modal. */
    persistent?: boolean;
    /** Modal width. Defaults to 660px. */
    width?: string | number;
    /** Disables the close button when true. */
    loading?: boolean;
  }>(),
  {
    persistent: true,
    width: 660,
    loading: false,
  },
);

const emit = defineEmits<{
  (e: 'close'): void;
}>();

const handleClose = () => {
  if (!loading) {
    emit('close');
  }
};

const handleDialogVisibility = (isVisible: boolean) => {
  if (!isVisible) {
    handleClose();
  }
};
</script>

<template>
  <v-dialog
    :model-value="true"
    @update:model-value="handleDialogVisibility"
    :width="width"
    :max-width="`calc(100vw - 24px)`"
    :persistent="persistent"
  >
    <div class="ua-modal">
      <div class="ua-modal__header">
        <span class="ua-modal__title">{{ title }}</span>
        <v-btn class="ua-modal__close-btn" variant="text" @click="handleClose" :disabled="loading">
          <v-icon icon="mdi-close" size="20" class="mr-1" />
          Close
        </v-btn>
      </div>
      <div class="ua-modal__header-strip" />

      <slot name="alerts" />

      <div class="ua-modal__body">
        <slot />
      </div>

      <div v-if="$slots.actions" class="ua-modal__actions">
        <slot name="actions" />
      </div>
    </div>
  </v-dialog>
</template>

<style scoped>
.ua-modal {
  width: 100%;
  border-radius: var(--ua-card-border-radius);
  overflow: hidden;
  background: var(--ua-card-body-bg);
  display: flex;
  flex-direction: column;
  max-height: calc(100vh - 48px);
}

.ua-modal__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--ua-spacing-sm) var(--ua-spacing-xl);
  background: var(--ua-card-header-bg);
  color: var(--ua-card-header-color);
}

.ua-modal__title {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
}

.ua-modal__close-btn {
  color: var(--ua-card-header-color);
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-semibold);
}

.ua-modal__header-strip {
  background: var(--ua-border-color);
  height: 4px;
}

.ua-modal__body {
  padding: var(--ua-spacing-lg);
  overflow-y: auto;
  flex: 1 1 auto;
  min-height: 0;
}

.ua-modal__actions {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--ua-spacing-md);
  padding: var(--ua-spacing-lg) var(--ua-spacing-xl) var(--ua-spacing-xl);
}
</style>
