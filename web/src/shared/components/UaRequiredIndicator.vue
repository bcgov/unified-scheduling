<template>
  <span class="ua-required-indicator" aria-hidden="true">*</span>
  <span class="ua-required-indicator__text"> (required)</span>
</template>

<style scoped>
.ua-required-indicator {
  color: rgb(var(--v-theme-error));
  font-weight: var(--ua-font-weight-bold);
  margin-left: var(--ua-spacing-xs);
}

.ua-required-indicator__text {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
  border: 0;
}
</style>
