<script setup lang="ts">
import type {
  LocationDto,
  StatCategoryResponse,
  SubCategoryResponse,
  UserResponse,
} from '@/api-access/generated/models';
import UaBtn from '@/shared/components/UaBtn.vue';
import { computed } from 'vue';

const props = defineProps<{
  locations: LocationDto[];
  employees: UserResponse[];
  categories: StatCategoryResponse[];
  subCategories: SubCategoryResponse[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  apply: [];
}>();

const locationId = defineModel<number | null>('locationId', { default: null });
const employeeId = defineModel<string | null>('employeeId', { default: null });
const categoryId = defineModel<number | null>('categoryId', { default: null });
const subCategoryId = defineModel<number | null>('subCategoryId', { default: null });
const status = defineModel<string | null>('status', { default: null });

const locationItems = computed(() =>
  props.locations
    .map((l) => ({ title: l.name ?? '', value: l.id ?? 0 }))
    .sort((a, b) => a.title.localeCompare(b.title)),
);

const employeeItems = computed(() =>
  props.employees
    .map((u) => ({ title: `${u.firstName} ${u.lastName}`.trim(), value: u.id }))
    .sort((a, b) => a.title.localeCompare(b.title)),
);

const categoryItems = computed(() => {
  const seen = new Set<string>();
  return props.categories
    .filter((c) => {
      const name = c.name ?? '';
      if (seen.has(name)) return false;
      seen.add(name);
      return true;
    })
    .map((c) => ({ title: c.name ?? '', value: c.id ?? 0 }))
    .sort((a, b) => a.title.localeCompare(b.title));
});

const filteredSubCategoryItems = computed(() => {
  const subs = categoryId.value
    ? props.subCategories.filter((sc) => sc.categoryId === categoryId.value)
    : props.subCategories;
  return subs.map((sc) => ({ title: sc.name ?? '', value: sc.id ?? 0 })).sort((a, b) => a.title.localeCompare(b.title));
});

const statusItems = [
  { title: 'Draft', value: 'Draft' },
  { title: 'Submitted', value: 'Submitted' },
];

function clearAll() {
  locationId.value = null;
  employeeId.value = null;
  categoryId.value = null;
  subCategoryId.value = null;
  status.value = null;
  emit('apply');
}
</script>

<template>
  <div class="filters-panel">
    <h3 class="filters-title">Filters</h3>

    <div class="filter-group">
      <label class="filter-label">Location</label>
      <v-select
        v-model="locationId"
        :items="locationItems"
        item-title="title"
        item-value="value"
        placeholder="Select an option"
        clearable
        hide-details
        density="compact"
      />
    </div>

    <div class="filter-group">
      <label class="filter-label">Employee</label>
      <v-select
        v-model="employeeId"
        :items="employeeItems"
        item-title="title"
        item-value="value"
        placeholder="Select an option"
        clearable
        hide-details
        density="compact"
      />
    </div>

    <div class="filter-group">
      <label class="filter-label">Work Area</label>
      <v-select
        v-model="categoryId"
        :items="categoryItems"
        item-title="title"
        item-value="value"
        placeholder="Select an option"
        clearable
        hide-details
        density="compact"
        @update:model-value="subCategoryId = null"
      />
    </div>

    <div class="filter-group">
      <label class="filter-label">Subcategory</label>
      <v-select
        v-model="subCategoryId"
        :items="filteredSubCategoryItems"
        item-title="title"
        item-value="value"
        placeholder="Select an option"
        clearable
        hide-details
        density="compact"
      />
    </div>

    <div class="filter-group">
      <label class="filter-label">Status</label>
      <v-select
        v-model="status"
        :items="statusItems"
        item-title="title"
        item-value="value"
        placeholder="Select an option"
        clearable
        hide-details
        density="compact"
      />
    </div>

    <div class="filter-actions">
      <UaBtn color="primary" variant="flat" block :loading="loading" @click="emit('apply')">Apply Filters</UaBtn>
      <UaBtn variant="text" block @click="clearAll">Clear</UaBtn>
    </div>
  </div>
</template>

<style scoped>
.filters-panel {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-md);
  padding: var(--ua-spacing-lg);
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  background: rgb(var(--v-theme-surface));
}

.filters-title {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
  margin: 0;
}

.filter-group {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xs);
}

.filter-label {
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
  color: var(--ua-text-primary);
}

.filter-actions {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xs);
  margin-top: var(--ua-spacing-sm);
}
</style>
