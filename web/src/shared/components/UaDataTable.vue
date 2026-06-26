<script setup lang="ts">
defineOptions({
  inheritAttrs: false,
});
</script>

<template>
  <v-data-table class="ua-data-table" v-bind="$attrs">
    <template v-for="(_, slotName) in $slots" #[slotName]="slotProps">
      <slot :name="slotName" v-bind="slotProps ?? {}" />
    </template>
  </v-data-table>
</template>

<style scoped>
.ua-data-table {
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius-sm);
  overflow: hidden;
  background-color: rgb(var(--v-theme-surface));
}

.ua-data-table :deep(thead th) {
  background-color: rgba(var(--v-theme-surface-variant), 0.35);
  color: var(--ua-text-primary);
  font-weight: var(--ua-font-weight-bold);
}
</style>
