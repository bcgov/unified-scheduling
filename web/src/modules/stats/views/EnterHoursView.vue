<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';

const props = defineProps<{
  /** 1 = Non-Supervision, 2 = Supervision. When set, locks all assignments to that group. */
  groupId?: number;
}>();

const formTitle = computed(() => {
  if (props.groupId === 1) return 'Enter Non-Supervision Hours';
  if (props.groupId === 2) return 'Enter Supervision Hours';
  return 'Enter Hours Worked';
});
import { mdiPlus } from '@mdi/js';
import {
  getApiStatsGroups,
  getApiStatsCategories,
  getApiStatsSubCategories,
  getApiStatsMetrics,
  getApiStatsSubCategoryMetrics,
  postApiStatsRecordsBatch,
  type StatGroupResponse,
  type StatCategoryResponse,
  type SubCategoryResponse,
  type StatMetricResponse,
  type SubCategoryMetricResponse,
  type StatRecordRequest,
} from '@/api-access/stats';
import Select from '@/shared/components/Select.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import type { SelectValue } from '@/types/select';
import AssignmentRow from '../components/AssignmentRow.vue';
import type { AssignmentData, PeriodType } from '../types';

// ── Reference data ─────────────────────────────────────────────────────────

const isLoadingReference = ref(true);
const groups = ref<StatGroupResponse[]>([]);
const categories = ref<StatCategoryResponse[]>([]);
const subCategories = ref<SubCategoryResponse[]>([]);
const metrics = ref<StatMetricResponse[]>([]);
const subCategoryMetrics = ref<SubCategoryMetricResponse[]>([]);

onMounted(async () => {
  const [groupsRes, catsRes, subCatsRes, metricsRes, scmRes] = await Promise.all([
    getApiStatsGroups(),
    getApiStatsCategories(),
    getApiStatsSubCategories(),
    getApiStatsMetrics(),
    getApiStatsSubCategoryMetrics(),
  ]);
  groups.value = groupsRes.data.value ?? [];
  categories.value = catsRes.data.value ?? [];
  subCategories.value = subCatsRes.data.value ?? [];
  metrics.value = metricsRes.data.value ?? [];
  subCategoryMetrics.value = scmRes.data.value ?? [];
  isLoadingReference.value = false;
});

// ── Locations ──────────────────────────────────────────────────────────────

const locationsStore = useLocationsStore();
const locationOptions = computed(() => locationsStore.getSelectOptions());

// ── Form state ─────────────────────────────────────────────────────────────

const selectedLocationId = ref<number | null>(null);
const periodType = ref<PeriodType>('Monthly');

const periodOptions: { label: string; value: PeriodType }[] = [
  { label: 'Daily', value: 'Daily' },
  { label: 'Weekly', value: 'Weekly' },
  { label: 'Monthly', value: 'Monthly' },
];

let nextId = 1;
const createAssignment = (): AssignmentData => ({
  id: String(nextId++),
  groupId: props.groupId ?? null,
  categoryId: null,
  subCategoryId: null,
  metricValues: {},
  comment: '',
});

const assignments = ref<AssignmentData[]>([createAssignment()]);

const isSubmitting = ref(false);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});
const successMessage = ref('');

// ── Date range ─────────────────────────────────────────────────────────────

const toDateStr = (date: Date): string => {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
};

const dateRange = computed((): { dateFrom: string; dateTo: string } => {
  const today = new Date();
  if (periodType.value === 'Daily') {
    const s = toDateStr(today);
    return { dateFrom: s, dateTo: s };
  }
  if (periodType.value === 'Weekly') {
    const day = today.getDay(); // 0 = Sunday
    const monday = new Date(today);
    monday.setDate(today.getDate() - (day === 0 ? 6 : day - 1));
    const sunday = new Date(monday);
    sunday.setDate(monday.getDate() + 6);
    return { dateFrom: toDateStr(monday), dateTo: toDateStr(sunday) };
  }
  // Monthly
  const first = new Date(today.getFullYear(), today.getMonth(), 1);
  const last = new Date(today.getFullYear(), today.getMonth() + 1, 0);
  return { dateFrom: toDateStr(first), dateTo: toDateStr(last) };
});

const formatDisplayDate = (dateStr: string): string => {
  const [y, m, d] = dateStr.split('-').map(Number);
  return new Date(y, m - 1, d).toLocaleDateString('en-CA', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
};

const dateRangeLabel = computed(() => {
  const { dateFrom, dateTo } = dateRange.value;
  if (dateFrom === dateTo) return formatDisplayDate(dateFrom);
  return `${formatDisplayDate(dateFrom)} – ${formatDisplayDate(dateTo)}`;
});

// ── Assignment management ──────────────────────────────────────────────────

const addAssignment = () => {
  assignments.value = [...assignments.value, createAssignment()];
};

const removeAssignment = (id: string) => {
  assignments.value = assignments.value.filter((a) => a.id !== id);
};

const onLocationChange = (value: SelectValue) => {
  selectedLocationId.value = value !== null && value !== undefined ? Number(value) : null;
};

// ── Submission ─────────────────────────────────────────────────────────────

const buildRecords = (status: string): StatRecordRequest[] | null => {
  const errors: Record<string, string> = {};

  if (!selectedLocationId.value) {
    errors['location'] = 'Location is required';
  }

  for (const [i, assignment] of assignments.value.entries()) {
    if (!assignment.groupId && !props.groupId) {
      errors[`assignment_${i}_group`] = 'Group is required';
    }
    if (!assignment.categoryId) {
      errors[`assignment_${i}_category`] = 'Work Area is required';
    }
    if (!assignment.subCategoryId) {
      errors[`assignment_${i}_subCategory`] = 'Subcategory is required';
    }

    const scms = subCategoryMetrics.value.filter((scm) => scm.subCategoryId === assignment.subCategoryId);
    for (const scm of scms) {
      const raw = assignment.metricValues[scm.id];
      if (raw && raw.trim() !== '' && isNaN(parseFloat(raw))) {
        errors[`assignment_${i}_metric_${scm.id}`] = 'Must be a valid number';
      }
    }
  }

  formErrors.value = errors;

  if (Object.keys(errors).length > 0) return null;

  const records: StatRecordRequest[] = [];
  const { dateFrom, dateTo } = dateRange.value;

  for (const assignment of assignments.value) {
    const scms = subCategoryMetrics.value.filter((scm) => scm.subCategoryId === assignment.subCategoryId);
    for (const scm of scms) {
      const raw = assignment.metricValues[scm.id];
      if (!raw || raw.trim() === '') continue;
      records.push({
        dateFrom,
        dateTo,
        periodType: periodType.value,
        locationId: selectedLocationId.value!,
        subCategoryMetricId: scm.id,
        value: parseFloat(raw),
        comment: assignment.comment || undefined,
        status,
      });
    }
  }

  if (records.length === 0) {
    formErrors.value = { form: 'Please enter at least one metric value before saving.' };
    return null;
  }

  return records;
};

const handleSave = async (status: string) => {
  successMessage.value = '';
  apiError.value = '';

  const records = buildRecords(status);
  if (!records) return;

  isSubmitting.value = true;
  try {
    const { data, error } = await postApiStatsRecordsBatch(records);
    if (error.value) {
      apiError.value = error.value.message || 'Failed to save records. Please try again.';
      return;
    }
    const label = status === 'Submitted' ? 'submitted' : 'saved as draft';
    successMessage.value = `${data.value?.length ?? records.length} record(s) ${label} successfully.`;
    // Reset form
    assignments.value = [createAssignment()];
    selectedLocationId.value = null;
  } catch (err) {
    apiError.value = err instanceof Error ? err.message : 'An unexpected error occurred.';
  } finally {
    isSubmitting.value = false;
  }
};
</script>

<template>
  <div class="enter-hours-page">
    <!-- Page header -->
    <div class="page-header">
      <h2 class="page-title">{{ formTitle }}</h2>
    </div>

    <div v-if="isLoadingReference" class="loading-state">Loading reference data…</div>

    <template v-else>
      <!-- Alerts -->
      <v-alert
        v-if="apiError"
        type="error"
        density="compact"
        class="page-alert"
        closable
        @click:close="apiError = ''"
      >
        {{ apiError }}
      </v-alert>
      <v-alert
        v-if="successMessage"
        type="success"
        density="compact"
        class="page-alert"
        closable
        @click:close="successMessage = ''"
      >
        {{ successMessage }}
      </v-alert>

      <!-- Location + Period card -->
      <v-card class="form-card" variant="outlined">
        <v-card-text>
          <div class="header-grid">
            <!-- Location -->
            <label class="field-label" for="location-select">Location</label>
            <div>
              <Select
                id="location-select"
                label="Select Location"
                :items="locationOptions"
                :model-value="selectedLocationId"
                :error-messages="formErrors['location']"
                @update:model-value="onLocationChange"
              />
            </div>

            <!-- Period type -->
            <label class="field-label">Period</label>
            <div class="period-row">
              <v-btn-toggle
                v-model="periodType"
                mandatory
                density="compact"
                variant="outlined"
                color="primary"
              >
                <v-btn
                  v-for="opt in periodOptions"
                  :key="opt.value"
                  :value="opt.value"
                  size="small"
                >
                  {{ opt.label }}
                </v-btn>
              </v-btn-toggle>
            </div>

            <!-- Date range display -->
            <label class="field-label">Date Range</label>
            <span class="date-range-display">{{ dateRangeLabel }}</span>
          </div>
        </v-card-text>
      </v-card>

      <!-- Assignment rows -->
      <div class="assignments-section">
        <div class="assignments-header">
          <span class="section-label">Work Assignments</span>
        </div>

        <div v-if="assignments.length === 0" class="empty-assignments">
          No assignments added. Click "Add Assignment" to begin.
        </div>

        <AssignmentRow
          v-for="(assignment, i) in assignments"
          :key="assignment.id"
          v-model="assignments[i]"
          :groups="groups"
          :categories="categories"
          :sub-categories="subCategories"
          :sub-category-metrics="subCategoryMetrics"
          :metrics="metrics"
          :index="i"
          :errors="formErrors"
          :fixed-group-id="groupId"
          @remove="removeAssignment(assignment.id)"
        />

        <v-btn
          variant="outlined"
          class="add-assignment-btn"
          :prepend-icon="mdiPlus"
          @click="addAssignment"
        >
          Add Assignment
        </v-btn>
      </div>

      <!-- Form-level error -->
      <p v-if="formErrors['form']" class="form-level-error">{{ formErrors['form'] }}</p>

      <!-- Actions -->
      <div class="form-actions">
        <v-btn variant="outlined" :disabled="isSubmitting" @click="$router.back()">Back</v-btn>
        <v-btn
          variant="outlined"
          color="primary"
          :loading="isSubmitting"
          @click="handleSave('Draft')"
        >
          Save Draft
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          :loading="isSubmitting"
          @click="handleSave('Submitted')"
        >
          Submit
        </v-btn>
      </div>
    </template>
  </div>
</template>

<style scoped>
.enter-hours-page {
  padding: 2rem;
  max-width: 860px;
}

.page-header {
  display: flex;
  align-items: center;
  margin-bottom: 1.5rem;
}

.page-title {
  font-size: 1.4rem;
  font-weight: 700;
  color: #1b2740;
}

.loading-state {
  padding: 2rem;
  color: #6b6b6b;
}

.page-alert {
  margin-bottom: 1rem;
}

.form-card {
  border-radius: 8px;
  border-color: #d0d0d2;
  margin-bottom: 1.5rem;
}

.header-grid {
  display: grid;
  grid-template-columns: 140px 1fr;
  gap: 0.85rem 1rem;
  align-items: center;
}

.field-label {
  font-size: 0.95rem;
  font-weight: 600;
  color: #1b2740;
}

.period-row {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.date-range-display {
  font-size: 0.95rem;
  color: #3a3a3a;
}

.assignments-section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.assignments-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.section-label {
  font-size: 1rem;
  font-weight: 700;
  color: #1b2740;
}

.empty-assignments {
  font-size: 0.9rem;
  color: #6b6b6b;
  padding: 1rem 0;
}

.add-assignment-btn {
  align-self: flex-start;
  text-transform: none;
  letter-spacing: 0;
}

.form-level-error {
  font-size: 0.9rem;
  color: #b00020;
  margin-bottom: 1rem;
}

.form-actions {
  display: flex;
  gap: 0.75rem;
  justify-content: flex-end;
}

.form-actions .v-btn {
  text-transform: none;
  letter-spacing: 0;
  min-width: 120px;
}
</style>
