<script setup lang="ts">
import { mdiMagnify } from '@mdi/js';
import { computed, useId } from 'vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import { useCalendarStore } from '../../calendarStore';
import CalendarMatrixSidePanelItem from './CalendarMatrixSidePanelItem.vue';
import type {
  CalendarMatrixDragPayload,
  CalendarMatrixSidePanel,
  CalendarMatrixSidePanelItem as CalendarMatrixSidePanelItemModel,
} from './calendarMatrixTypes';

const props = defineProps<{
  panel: CalendarMatrixSidePanel;
}>();

const calendarStore = useCalendarStore();
const searchId = useId();
const searchQuery = computed(() => calendarStore.sidePanelSearchByView[calendarStore.activeViewId] ?? '');
const searchTerms = computed(() => searchQuery.value.toLowerCase().trim().split(/\s+/).filter(Boolean));
const displayedItems = computed(() => {
  if (!props.panel.search || searchTerms.value.length === 0) {
    return props.panel.items;
  }

  return props.panel.items.filter((item) => {
    const searchableText = [item.title, item.subtitle, item.searchText].filter(Boolean).join(' ').toLowerCase();
    return searchTerms.value.every((term) => searchableText.includes(term));
  });
});

function setSearchQuery(value: string | null) {
  calendarStore.setSidePanelSearch(calendarStore.activeViewId, value ?? '');
}

const emit = defineEmits<{
  (event: 'action'): void;
  (event: 'itemClick', payload: CalendarMatrixSidePanelItemModel): void;
  (event: 'itemDragStart', payload: CalendarMatrixDragPayload): void;
}>();
</script>

<template>
  <aside class="calendar-matrix-side-panel" :aria-label="panel.label">
    <div class="calendar-matrix-side-panel__controls">
      <header class="calendar-matrix-side-panel__header">
        <h2 class="calendar-matrix-side-panel__title">{{ panel.label }}</h2>
        <UaBtn
          v-if="panel.actionLabel"
          variant="outlined"
          size="small"
          :aria-label="panel.actionLabel"
          @click="emit('action')"
        >
          {{ panel.actionLabel }}
        </UaBtn>
      </header>
      <UaTextField
        v-if="panel.search"
        :id="searchId"
        :model-value="searchQuery"
        label=""
        :aria-label="`Search ${panel.label.toLowerCase()}`"
        :placeholder="panel.search.placeholder ?? 'Search items...'"
        :prepend-inner-icon="mdiMagnify"
        density="compact"
        clearable
        @update:model-value="setSearchQuery"
      />
    </div>

    <div v-if="displayedItems.length" class="calendar-matrix-side-panel__list" role="list">
      <CalendarMatrixSidePanelItem
        v-for="item in displayedItems"
        :key="item.id"
        :item="item"
        @item-click="emit('itemClick', $event)"
        @drag-start="emit('itemDragStart', $event)"
      >
        <template v-if="$slots.item" #default="slotProps">
          <slot name="item" v-bind="slotProps" />
        </template>
      </CalendarMatrixSidePanelItem>
    </div>
    <div v-else class="calendar-matrix-side-panel__empty">
      {{ panel.items.length ? 'No matching items.' : 'No items to display.' }}
    </div>
  </aside>
</template>

<style scoped>
.calendar-matrix-side-panel {
  background: rgb(var(--v-theme-surface));
  border: 1px solid var(--ua-border-color);
  color: var(--ua-text-primary);
  display: grid;
  gap: var(--ua-spacing-md);
  grid-template-rows: auto minmax(0, 1fr);
  min-height: 0;
  padding: var(--ua-spacing-md);
}

.calendar-matrix-side-panel__controls {
  display: grid;
  gap: var(--ua-spacing-sm);
}

.calendar-matrix-side-panel__header {
  align-items: center;
  display: flex;
  gap: var(--ua-spacing-sm);
  justify-content: space-between;
}

.calendar-matrix-side-panel__title {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-xs);
  margin: 0;
  text-transform: uppercase;
}

.calendar-matrix-side-panel__list {
  align-content: start;
  display: grid;
  gap: var(--ua-spacing-sm);
  min-height: 0;
  overflow: auto;
}

.calendar-matrix-side-panel__empty {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
}
</style>
