<script setup lang="ts" generic="T extends Record<PropertyKey, any>">
import { computed, nextTick, onBeforeUnmount, ref, useAttrs, watch } from 'vue';
import { useDraggable } from 'vue-draggable-plus';

type UaDataTableReorderPayload<TItem> = {
  item: TItem;
  newIndex: number;
  oldIndex: number;
};

defineOptions({
  inheritAttrs: false,
});

const props = withDefaults(
  defineProps<{
    items?: T[];
    loading?: boolean;
    paginate?: boolean;
    draggable?: boolean;
    searchable?: boolean;
    search?: string;
  }>(),
  {
    items: () => [],
    loading: false,
    paginate: true,
    draggable: false,
    searchable: false,
    search: undefined,
  },
);

const emit = defineEmits<{
  (e: 'reorder', payload: UaDataTableReorderPayload<T>): void;
  (e: 'update:search', value: string): void;
}>();

const attrs = useAttrs();
const rootElement = ref<HTMLElement | null>(null);
const localItems = ref<T[]>([...props.items]);
const localSearch = ref('');
const draggableInstance = ref<ReturnType<typeof useDraggable<T>> | null>(null);
const searchQuery = computed({
  get: () => props.search ?? localSearch.value,
  set: (value: string) => {
    localSearch.value = value;
    emit('update:search', value);
  },
});
const hasActiveSearch = computed(() => searchQuery.value.trim().length > 0);
const isDraggableEnabled = computed(() => props.draggable && !props.paginate && !hasActiveSearch.value);

const forwardedAttrs = computed(() => {
  const forwarded = { ...attrs };
  delete forwarded.items;
  delete forwarded.loading;
  delete forwarded.page;
  delete forwarded['items-per-page'];
  delete forwarded.search;
  delete forwarded['search-placeholder'];
  return forwarded;
});

watch(
  () => props.items,
  (newItems) => {
    localItems.value = [...newItems];
  },
);

watch(
  () => props.search,
  (newSearch) => {
    if (newSearch != null) {
      localSearch.value = newSearch;
    }
  },
  { immediate: true },
);

const destroyDraggable = () => {
  draggableInstance.value?.destroy?.();
  draggableInstance.value = null;
};

const initializeDraggable = async () => {
  if (!isDraggableEnabled.value || props.loading) {
    destroyDraggable();
    return;
  }

  if (draggableInstance.value) {
    return;
  }

  await nextTick();

  const tableBody = rootElement.value?.querySelector('tbody') as HTMLElement | null;
  if (!tableBody) {
    return;
  }

  draggableInstance.value = useDraggable(tableBody, localItems, {
    handle: '.drag-handle',
    ghostClass: 'ua-sortable-ghost',
    dragClass: 'ua-sortable-drag',
    animation: 150,
    onUpdate: (event?: { oldIndex?: number | null; newIndex?: number | null }) => {
      if (event?.newIndex == null || event.oldIndex == null) {
        return;
      }

      const movedItem = localItems.value[event.newIndex];
      if (!movedItem) {
        return;
      }

      emit('reorder', {
        item: movedItem,
        newIndex: event.newIndex,
        oldIndex: event.oldIndex,
      });
    },
  });
};

watch(
  () => [isDraggableEnabled.value, props.loading] as const,
  () => {
    if (!isDraggableEnabled.value || props.loading) {
      destroyDraggable();
      return;
    }

    void initializeDraggable();
  },
  { immediate: true },
);

onBeforeUnmount(() => {
  destroyDraggable();
});
</script>

<template>
  <div ref="rootElement" class="ua-data-table-wrapper">
    <div v-if="searchable" class="ua-data-table__search">
      <v-text-field
        v-model="searchQuery"
        label="Search"
        placeholder="Search"
        prepend-inner-icon="mdi-magnify"
        clearable
        hide-details
        density="comfortable"
        variant="outlined"
      />
    </div>

    <v-data-table
      class="ua-data-table"
      :class="{ 'ua-data-table--draggable': isDraggableEnabled }"
      :items="localItems"
      :loading="loading"
      :search="searchQuery"
      :items-per-page="paginate ? undefined : -1"
      :hide-default-footer="!paginate"
      v-bind="forwardedAttrs"
    >
      <template v-for="(_, slotName) in $slots" #[slotName]="slotProps">
        <slot :name="slotName" v-bind="slotProps ?? {}" />
      </template>
    </v-data-table>
  </div>
</template>

<style scoped>
.ua-data-table-wrapper {
  width: 100%;
}

.ua-data-table__search {
  margin-bottom: var(--ua-spacing-md);
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

.ua-data-table--draggable :deep(tbody tr) {
  cursor: default;
}
</style>

<style>
.ua-sortable-ghost {
  opacity: 0.4;
  background-color: rgba(var(--v-theme-primary), 0.15);
}

.ua-sortable-drag {
  opacity: 0;
}
</style>
