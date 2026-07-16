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
    draggable?: boolean;
    draggableHandleSelector?: string;
  }>(),
  {
    items: () => [],
    loading: false,
    draggable: false,
    draggableHandleSelector: '.drag-handle',
  },
);

const emit = defineEmits<{
  (e: 'reorder', payload: UaDataTableReorderPayload<T>): void;
}>();

const attrs = useAttrs();
const rootElement = ref<HTMLElement | null>(null);
const localItems = ref<T[]>([...props.items]);
const draggableInstance = ref<ReturnType<typeof useDraggable<T>> | null>(null);

const forwardedAttrs = computed(() => {
  const forwarded = { ...attrs };
  delete forwarded.items;
  delete forwarded.loading;
  return forwarded;
});

watch(
  () => props.items,
  (newItems) => {
    localItems.value = [...newItems];
  },
);

const destroyDraggable = () => {
  draggableInstance.value?.destroy?.();
  draggableInstance.value = null;
};

const initializeDraggable = async () => {
  if (!props.draggable || props.loading) {
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
    handle: props.draggableHandleSelector,
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
  () => [props.draggable, props.loading] as const,
  () => {
    if (!props.draggable || props.loading) {
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
    <v-data-table
      class="ua-data-table"
      :class="{ 'ua-data-table--draggable': draggable }"
      :items="localItems"
      :loading="loading"
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
