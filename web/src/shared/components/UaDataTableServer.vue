<script setup lang="ts" generic="T extends Record<PropertyKey, any>">
import { useAttrs } from 'vue';

defineOptions({
  inheritAttrs: false,
});

withDefaults(
  defineProps<{
    items?: T[];
    loading?: boolean;
    // Total row count across all server pages, driving Vuetify's built-in footer/paginator.
    itemsLength: number;
  }>(),
  {
    items: () => [],
    loading: false,
  },
);

// Caller owns pagination state (page, items-per-page, etc.) via v-model / update events.
const attrs = useAttrs();
</script>

<template>
  <div class="ua-data-table-wrapper">
    <v-data-table-server
      class="ua-data-table"
      :items="items"
      :items-length="itemsLength"
      :loading="loading"
      v-bind="attrs"
    >
      <template v-for="(_, slotName) in $slots" #[slotName]="slotProps">
        <slot :name="slotName" v-bind="slotProps ?? {}" />
      </template>
    </v-data-table-server>
  </div>
</template>

<style scoped>
.ua-data-table-wrapper {
  width: 100%;
}

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
