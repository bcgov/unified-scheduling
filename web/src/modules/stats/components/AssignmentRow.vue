<script setup lang="ts">
import type {
  StatCategoryResponse,
  StatGroupResponse,
  StatMetricResponse,
  SubCategoryMetricResponse,
  SubCategoryResponse,
} from '@/api-access/stats';
import Select from '@/shared/components/Select.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import type { SelectValue } from '@/types/select';
import { computed } from 'vue';
import type { AssignmentData } from '../types';

const props = defineProps<{
  groups: StatGroupResponse[];
  categories: StatCategoryResponse[];
  subCategories: SubCategoryResponse[];
  subCategoryMetrics: SubCategoryMetricResponse[];
  metrics: StatMetricResponse[];
  index: number;
  errors: Record<string, string>;
  /** When set, the group dropdown is hidden and this group is used for all category filtering. */
  fixedGroupId?: number | null;
  /** Override the header background colour. Falls back to --ua-card-header-bg. */
  headerColor?: string;
}>();

const emit = defineEmits<{
  (e: 'remove'): void;
}>();

const model = defineModel<AssignmentData>({ required: true });

// ── Derived options ────────────────────────────────────────────────────────

const groupOptions = computed(() =>
  [...props.groups].sort((a, b) => a.displayOrder - b.displayOrder).map((g) => ({ code: g.id, description: g.name })),
);

const effectiveGroupId = computed(() => props.fixedGroupId ?? model.value.groupId);

const categoryOptions = computed(() =>
  props.categories
    .filter((c) => !c.isArchived && (effectiveGroupId.value === null || c.groupId === effectiveGroupId.value))
    .sort((a, b) => a.displayOrder - b.displayOrder)
    .map((c) => ({ code: c.id, description: c.name })),
);

const subCategoryOptions = computed(() => {
  if (!model.value.categoryId) return [];
  return props.subCategories
    .filter((sc) => sc.categoryId === model.value.categoryId)
    .sort((a, b) => a.displayOrder - b.displayOrder)
    .map((sc) => ({ code: sc.id, description: sc.name }));
});

const showSubCategorySelect = computed(() => subCategoryOptions.value.length > 1);

const metricDetails = computed(() => {
  if (!model.value.subCategoryId) return [];
  return props.subCategoryMetrics
    .filter((scm) => scm.subCategoryId === model.value.subCategoryId)
    .sort((a, b) => a.displayOrder - b.displayOrder)
    .map((scm) => {
      const metric = props.metrics.find((m) => m.id === scm.metricId);
      return { id: scm.id, name: metric?.name ?? '', unit: metric?.unitOfMeasure ?? '' };
    });
});

// ── Event handlers ─────────────────────────────────────────────────────────

const onGroupChange = (value: SelectValue | undefined) => {
  const newGroupId = value !== null && value !== undefined ? Number(value) : null;
  model.value = { ...model.value, groupId: newGroupId, categoryId: null, subCategoryId: null, metricValues: {} };
};

const onCategoryChange = (value: SelectValue | undefined) => {
  const newCategoryId = value !== null && value !== undefined ? Number(value) : null;
  if (!newCategoryId) {
    model.value = { ...model.value, categoryId: null, subCategoryId: null, metricValues: {} };
    return;
  }
  const subs = props.subCategories.filter((sc) => sc.categoryId === newCategoryId);
  const autoSubId = subs.length === 1 ? subs[0].id : null;
  model.value = { ...model.value, categoryId: newCategoryId, subCategoryId: autoSubId, metricValues: {} };
};

const onSubCategoryChange = (value: SelectValue | undefined) => {
  const newId = value !== null && value !== undefined ? Number(value) : null;
  model.value = { ...model.value, subCategoryId: newId, metricValues: {} };
};

const onMetricValueInput = (scmId: number, value: string) => {
  model.value = {
    ...model.value,
    metricValues: { ...model.value.metricValues, [scmId]: value },
  };
};

const onCommentInput = (value: string) => {
  model.value = { ...model.value, comment: value };
};
</script>

<template>
  <v-card class="assignment-card" variant="outlined">
    <div class="assignment-header" :style="headerColor ? { backgroundColor: headerColor } : undefined">
      <span class="assignment-title">Assignment {{ index + 1 }}</span>
      <v-btn variant="text" density="compact" color="white" class="remove-btn" @click="emit('remove')"> Remove </v-btn>
    </div>

    <v-card-text class="assignment-body">
      <UaFormGrid>
        <!-- Group — hidden when a fixed group is set at the form level -->
        <template v-if="!fixedGroupId">
          <label class="ua-form-label" :for="`group-${model.id}`">Group</label>
          <Select
            :id="`group-${model.id}`"
            label="Select Group"
            :items="groupOptions"
            :model-value="model.groupId"
            :error-messages="errors[`assignment_${index}_group`]"
            @update:model-value="onGroupChange"
          />
        </template>

        <!-- Work Area (Category) -->
        <label class="ua-form-label" :for="`category-${model.id}`">Work Area</label>
        <Select
          :id="`category-${model.id}`"
          label="Select Work Area"
          :items="categoryOptions"
          :model-value="model.categoryId"
          :error-messages="errors[`assignment_${index}_category`]"
          :disabled="!model.groupId"
          @update:model-value="onCategoryChange"
        />

        <!-- Subcategory — only shown when there are multiple options -->
        <template v-if="showSubCategorySelect">
          <label class="ua-form-label" :for="`subcategory-${model.id}`">Subcategory</label>
          <Select
            :id="`subcategory-${model.id}`"
            label="Select Subcategory"
            :items="subCategoryOptions"
            :model-value="model.subCategoryId"
            :error-messages="errors[`assignment_${index}_subCategory`]"
            @update:model-value="onSubCategoryChange"
          />
        </template>

        <!-- Dynamic metric inputs -->
        <template v-for="m in metricDetails" :key="m.id">
          <label class="ua-form-label" :for="`metric-${model.id}-${m.id}`">
            {{ m.name }}
            <span class="unit-label">({{ m.unit }})</span>
          </label>
          <v-text-field
            :id="`metric-${model.id}-${m.id}`"
            type="number"
            min="0"
            step="0.25"
            placeholder="0"
            hide-details="auto"
            :model-value="model.metricValues[m.id] ?? ''"
            :error-messages="errors[`assignment_${index}_metric_${m.id}`]"
            @update:model-value="(v) => onMetricValueInput(m.id, String(v))"
          />
        </template>

        <!-- Assignment-level error (no metrics entered) -->
        <template v-if="errors[`assignment_${index}`]">
          <span />
          <span class="field-error">{{ errors[`assignment_${index}`] }}</span>
        </template>

        <!-- Comment -->
        <label class="ua-form-label" :for="`comment-${model.id}`">Comment</label>
        <v-textarea
          :id="`comment-${model.id}`"
          rows="2"
          auto-grow
          hide-details="auto"
          placeholder="Optional note"
          :model-value="model.comment"
          @update:model-value="(v) => onCommentInput(String(v))"
        />
      </UaFormGrid>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.assignment-card {
  border-radius: var(--ua-border-radius);
  overflow: hidden;
  border-color: var(--ua-border-color);
}

.assignment-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--ua-spacing-sm) var(--ua-spacing-md) var(--ua-spacing-sm) var(--ua-spacing-lg);
  background: var(--ua-card-header-bg);
}

.assignment-title {
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-card-header-color);
}

.remove-btn {
  font-size: var(--ua-font-size-sm);
}

.assignment-body {
  padding: var(--ua-spacing-lg);
  background: var(--ua-card-body-bg);
}

.ua-form-label {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
  line-height: 1.4;
}

.unit-label {
  font-size: var(--ua-font-size-xs);
  font-weight: var(--ua-font-weight-normal);
  color: var(--ua-text-muted);
}

.field-error {
  font-size: var(--ua-font-size-sm);
  color: rgb(var(--v-theme-error));
}

:deep(.v-field) {
  border-radius: var(--ua-border-radius);
  background: var(--ua-field-bg);
}

@media (max-width: 640px) {
  .assignment-body {
    padding: var(--ua-spacing-md);
  }
}
</style>
