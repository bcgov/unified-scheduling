<script setup lang="ts">
import type { StatCategoryResponse, StatGroupResponse, SubCategoryResponse } from '@/api-access/generated/models';
import UaBtn from '@/shared/components/UaBtn.vue';
import type { SelectOption } from '@/types/select';
import { computed, ref } from 'vue';
import { EntryStatus } from '../constants';

const props = defineProps<{
  groups: StatGroupResponse[];
  employees: { id: string; name: string }[];
  locations: SelectOption[];
  categories: StatCategoryResponse[];
  subCategories: SubCategoryResponse[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  apply: [];
}>();

const groupId = defineModel<number | null>('groupId', { default: null });
const employeeId = defineModel<string | null>('employeeId', { default: null });
const locationId = defineModel<number | null>('locationId', { default: null });
const categoryName = defineModel<string | null>('categoryName', { default: null });
const subCategoryId = defineModel<number | null>('subCategoryId', { default: null });
const status = defineModel<string | null>('status', { default: null });
const fromDate = defineModel<string | null>('fromDate', { default: null });
const toDate = defineModel<string | null>('toDate', { default: null });

const groupItems = computed(() =>
  [...props.groups]
    .sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0))
    .map((g) => ({ title: g.name ?? '', value: g.id! })),
);

const employeeItems = computed(() =>
  props.employees.map((u) => ({ title: u.name, value: u.id })).sort((a, b) => a.title.localeCompare(b.title)),
);

const locationItems = computed(() =>
  props.locations
    .map((l) => ({ title: String(l.description ?? ''), value: Number(l.code) }))
    .sort((a, b) => a.title.localeCompare(b.title)),
);

// Filter categories by selected group, then dedup by name
const categoryItems = computed(() => {
  const seen = new Set<string>();
  return props.categories
    .filter((c) => {
      if (groupId.value != null && c.groupId !== groupId.value) return false;
      const name = c.name ?? '';
      if (seen.has(name)) return false;
      seen.add(name);
      return true;
    })
    .map((c) => ({ title: c.name ?? '', value: c.name ?? '' }))
    .sort((a, b) => a.title.localeCompare(b.title));
});

const matchingCategoryIds = computed(() => {
  if (!categoryName.value) return [];
  return props.categories.filter((c) => c.name === categoryName.value && c.id != null).map((c) => c.id!);
});

const filteredSubCategoryItems = computed(() => {
  const subs =
    matchingCategoryIds.value.length > 0
      ? props.subCategories.filter((sc) => sc.categoryId != null && matchingCategoryIds.value.includes(sc.categoryId))
      : props.subCategories;
  return subs
    .filter((sc) => sc.id != null)
    .map((sc) => ({ title: sc.name ?? '', value: sc.id! }))
    .sort((a, b) => a.title.localeCompare(b.title));
});

const statusItems = [
  { title: 'Draft', value: EntryStatus.Draft },
  { title: 'Submitted', value: EntryStatus.Submitted },
  { title: 'Signed Off', value: EntryStatus.SignedOff },
];

const groupError = ref(false);

function onGroupChange() {
  groupError.value = false;
  categoryName.value = null;
  subCategoryId.value = null;
}

function applyFilters() {
  if (!groupId.value) {
    groupError.value = true;
    return;
  }
  groupError.value = false;
  emit('apply');
}

function clearAll() {
  groupId.value = null;
  employeeId.value = null;
  locationId.value = null;
  categoryName.value = null;
  subCategoryId.value = null;
  status.value = null;
  fromDate.value = null;
  toDate.value = null;
  groupError.value = false;
  emit('apply');
}
</script>

<template>
  <div class="filters-panel">
    <h3 class="filters-title">Filters</h3>

    <div class="filter-group">
      <label class="filter-label">
        Group
        <span class="filter-required">*</span>
      </label>
      <v-select
        v-model="groupId"
        :items="groupItems"
        item-title="title"
        item-value="value"
        placeholder="Select a group"
        :error-messages="groupError ? 'Group is required.' : []"
        density="compact"
        @update:model-value="onGroupChange"
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
      <label class="filter-label">Work Area</label>
      <v-select
        v-model="categoryName"
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
      <label class="filter-label">From Date</label>
      <v-text-field v-model="fromDate" type="date" clearable hide-details density="compact" />
    </div>

    <div class="filter-group">
      <label class="filter-label">To Date</label>
      <v-text-field v-model="toDate" type="date" clearable hide-details density="compact" />
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
      <UaBtn color="primary" variant="flat" block :loading="loading" @click="applyFilters">Apply Filters</UaBtn>
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

.filter-required {
  color: rgb(var(--v-theme-error));
  margin-left: 2px;
}

.filter-actions {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xs);
  margin-top: var(--ua-spacing-sm);
}
</style>
