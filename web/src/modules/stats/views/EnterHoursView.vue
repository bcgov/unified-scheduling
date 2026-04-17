<script setup lang="ts">
import {
  getApiStatsCategories,
  getApiStatsGroups,
  getApiStatsMetrics,
  getApiStatsSubCategories,
  getApiStatsSubCategoryMetrics,
  postApiStatsRecordsBatch,
  type StatCategoryResponse,
  type StatGroupResponse,
  type StatMetricResponse,
  type StatRecordRequest,
  type SubCategoryMetricResponse,
  type SubCategoryResponse,
} from '@/api-access/stats';
import Select from '@/shared/components/Select.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import type { SelectValue } from '@/types/select';
import { mdiPlus } from '@mdi/js';
import { computed, onMounted, ref, watch } from 'vue';
import AssignmentRow from '../components/AssignmentRow.vue';
import type { AssignmentData, PeriodType } from '../types';

const props = defineProps<{
  /** 1 = Non-Supervision, 2 = Supervision. When set, locks all assignments to that group. */
  groupId?: number;
}>();

const formTitle = computed(() => {
  if (props.groupId === 1) return 'Enter Non-Supervision Hours';
  if (props.groupId === 2) return 'Enter Supervision Hours';
  return 'Enter Hours Worked';
});

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

// Daily / Monthly anchor
const anchorDate = ref('');
// Weekly — free dateFrom/dateTo, capped at 7 days
const weeklyFrom = ref('');
const weeklyTo = ref('');

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

const addDays = (dateStr: string, days: number): string => {
  const d = new Date(dateStr + 'T00:00:00');
  d.setDate(d.getDate() + days);
  return toDateStr(d);
};

const diffDays = (from: string, to: string): number => {
  const a = new Date(from + 'T00:00:00');
  const b = new Date(to + 'T00:00:00');
  return Math.round((b.getTime() - a.getTime()) / 86400000);
};

const today = new Date();
anchorDate.value = toDateStr(today);
weeklyFrom.value = toDateStr(today);
weeklyTo.value = addDays(toDateStr(today), 6);

// When weekly dateFrom changes → auto-set dateTo to +6 days
watch(weeklyFrom, (val) => {
  if (val) weeklyTo.value = addDays(val, 6);
});

// When weekly dateTo changes → cap to 7 days from dateFrom
watch(weeklyTo, (val) => {
  if (val && weeklyFrom.value && diffDays(weeklyFrom.value, val) > 6) {
    weeklyTo.value = addDays(weeklyFrom.value, 6);
  }
});

const dateRange = computed((): { dateFrom: string; dateTo: string } => {
  if (periodType.value === 'Daily') {
    return { dateFrom: anchorDate.value, dateTo: anchorDate.value };
  }
  if (periodType.value === 'Weekly') {
    return { dateFrom: weeklyFrom.value, dateTo: weeklyTo.value };
  }
  // Monthly — anchorDate is "YYYY-MM"
  const [y, m] = anchorDate.value.split('-').map(Number);
  const first = new Date(y, m - 1, 1);
  const last = new Date(y, m, 0);
  return { dateFrom: toDateStr(first), dateTo: toDateStr(last) };
});

// Reset dates when switching period types
watch(periodType, () => {
  const t = new Date();
  anchorDate.value =
    periodType.value === 'Monthly' ? `${t.getFullYear()}-${String(t.getMonth() + 1).padStart(2, '0')}` : toDateStr(t);
  weeklyFrom.value = toDateStr(t);
  weeklyTo.value = addDays(toDateStr(t), 6);
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
      if (!raw || raw.trim() === '') continue;

      const val = parseFloat(raw);
      if (isNaN(val)) {
        errors[`assignment_${i}_metric_${scm.id}`] = 'Must be a valid number';
        continue;
      }

      const metric = metrics.value.find((m) => m.id === scm.metricId);
      const isRegularHours = metric?.unitOfMeasure === 'hours' && !metric.name.toLowerCase().includes('overtime');
      if (isRegularHours) {
        if (periodType.value === 'Daily' && val > 7) {
          errors[`assignment_${i}_metric_${scm.id}`] = 'Daily hours cannot exceed 7';
        } else if (periodType.value === 'Weekly' && val > 35) {
          errors[`assignment_${i}_metric_${scm.id}`] = 'Weekly hours cannot exceed 35';
        }
      }
    }
  }

  formErrors.value = errors;

  if (Object.keys(errors).length > 0) return null;

  const records: StatRecordRequest[] = [];
  const { dateFrom: dateFromVal, dateTo: dateToVal } = dateRange.value;

  for (const assignment of assignments.value) {
    const scms = subCategoryMetrics.value.filter((scm) => scm.subCategoryId === assignment.subCategoryId);
    for (const scm of scms) {
      const raw = assignment.metricValues[scm.id];
      if (!raw || raw.trim() === '') continue;
      records.push({
        dateFrom: dateFromVal,
        dateTo: dateToVal,
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
    const { error } = await postApiStatsRecordsBatch(records);
    if (error.value) {
      apiError.value = error.value.message || 'Failed to save records. Please try again.';
      return;
    }
    const label = status === 'Submitted' ? 'submitted' : 'saved as draft';
    successMessage.value = `Records ${label} successfully.`;
  } catch (err) {
    apiError.value = err instanceof Error ? err.message : 'An unexpected error occurred.';
  } finally {
    isSubmitting.value = false;
  }
};
</script>

<template>
  <div class="enter-hours-page">
    <div class="page-card">
      <!-- Green header -->
      <div class="page-header">
        <span class="page-title">{{ formTitle }}</span>
      </div>
      <div class="header-strip" />

      <div class="page-body">
        <div v-if="isLoadingReference" class="loading-state">Loading reference data…</div>

        <template v-else>
          <!-- Alerts -->
          <v-alert v-if="apiError" type="error" density="compact" class="page-alert" closable
            @click:close="apiError = ''">
            {{ apiError }}
          </v-alert>
          <v-alert v-if="successMessage" type="success" density="compact" class="page-alert" closable
            @click:close="successMessage = ''">
            {{ successMessage }}
          </v-alert>

          <!-- Location + Period -->
          <div class="form-grid">
            <label class="form-field-label" for="location-select">Location</label>
            <Select id="location-select" label="Select Location" :items="locationOptions"
              :model-value="selectedLocationId" :error-messages="formErrors['location']"
              @update:model-value="onLocationChange" />

            <label class="form-field-label">Period</label>
            <div class="period-row">
              <v-btn-toggle v-model="periodType" mandatory density="compact" variant="outlined" color="primary">
                <v-btn v-for="opt in periodOptions" :key="opt.value" :value="opt.value" size="small">
                  {{ opt.label }}
                </v-btn>
              </v-btn-toggle>
            </div>

            <template v-if="periodType === 'Weekly'">
              <label class="form-field-label">Date From</label>
              <v-text-field v-model="weeklyFrom" type="date" hide-details />
              <label class="form-field-label">Date To</label>
              <v-text-field v-model="weeklyTo" type="date" :min="weeklyFrom" :max="addDays(weeklyFrom, 6)"
                hide-details />
            </template>
            <template v-else>
              <label class="form-field-label">{{ periodType === 'Monthly' ? 'Month' : 'Date' }}</label>
              <v-text-field v-model="anchorDate" :type="periodType === 'Monthly' ? 'month' : 'date'" hide-details />
            </template>
          </div>

          <div class="section-divider" />

          <!-- Assignment rows -->
          <div class="assignments-section">
            <span class="section-label">Work Assignments</span>

            <div v-if="assignments.length === 0" class="empty-assignments">
              No assignments added. Click "Add Assignment" to begin.
            </div>

            <AssignmentRow v-for="(assignment, i) in assignments" :key="assignment.id" v-model="assignments[i]"
              :groups="groups" :categories="categories" :sub-categories="subCategories"
              :sub-category-metrics="subCategoryMetrics" :metrics="metrics" :index="i" :errors="formErrors"
              :fixed-group-id="groupId" @remove="removeAssignment(assignment.id)" />

            <v-btn variant="outlined" class="add-assignment-btn" :prepend-icon="mdiPlus" @click="addAssignment">
              Add Assignment
            </v-btn>
          </div>

          <!-- Form-level error -->
          <p v-if="formErrors['form']" class="form-level-error">{{ formErrors['form'] }}</p>

          <!-- Actions -->
          <div class="form-actions">
            <v-btn variant="outlined" :disabled="isSubmitting" @click="$router.back()">Back</v-btn>
            <v-btn variant="outlined" color="primary" :loading="isSubmitting" @click="handleSave('Draft')">
              Save Draft
            </v-btn>
            <v-btn color="primary" variant="flat" :loading="isSubmitting" @click="handleSave('Submitted')">
              Submit
            </v-btn>
          </div>
        </template>
      </div>
    </div>
  </div>
</template>

<style scoped>
.enter-hours-page {
  padding: 2rem;
  max-width: 900px;
}

.page-card {
  border-radius: 12px;
  overflow: hidden;
  background: #e9e9eb;
}

.page-header {
  display: flex;
  align-items: center;
  padding: 0.6rem 1.4rem;
  background: #5f8f2c;
  color: #fff;
}

.page-title {
  font-size: 1.15rem;
  font-weight: 700;
}

.header-strip {
  background: #d0d0d2;
  height: 4px;
}

.page-body {
  padding: 1.4rem;
}

.loading-state {
  padding: 1rem 0;
  color: #6b6b6b;
}

.page-alert {
  margin-bottom: 1rem;
}

.form-grid {
  display: grid;
  grid-template-columns: 210px 1fr;
  gap: 1rem;
  align-items: center;
  margin-bottom: 1rem;
}

.form-field-label {
  font-size: 1.15rem;
  font-weight: 700;
  color: #1b2740;
}

.period-row {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex-wrap: wrap;
}

.section-divider {
  border-top: 1px solid #d0d0d2;
  margin: 1.2rem 0;
}

.assignments-section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.section-label {
  font-size: 1.15rem;
  font-weight: 700;
  color: #1b2740;
}

.empty-assignments {
  font-size: 0.9rem;
  color: #6b6b6b;
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
  padding-top: 0.5rem;
}

.form-actions .v-btn {
  text-transform: none;
  letter-spacing: 0;
  min-width: 120px;
}

:deep(.v-field) {
  border-radius: 8px;
  background: #efeff1;
}

@media (max-width: 640px) {
  .enter-hours-page {
    padding: 1rem;
  }

  .form-grid {
    grid-template-columns: 1fr;
    gap: 0.4rem 0;
  }

  .form-field-label {
    font-size: 1rem;
    margin-bottom: 0;
  }

  .form-actions {
    flex-wrap: wrap;
  }

  .form-actions .v-btn {
    flex: 1 1 auto;
    min-width: 0;
  }
}
</style>
