<script setup lang="ts">
import type {
  StatCategoryResponse,
  StatGroupResponse,
  StatMetricResponse,
  SubCategoryMetricResponse,
  SubCategoryResponse,
  UserResponse,
} from '@/api-access/generated/models';
import { Permissions } from '@/api-access/generated/models';
import { getApiStatsCategories } from '@/api-access/generated/stat-categories/stat-categories';
import { getApiStatsGroups } from '@/api-access/generated/stat-groups/stat-groups';
import { getApiStatsMetrics } from '@/api-access/generated/stat-metrics/stat-metrics';
import { getApiStatsSubCategories } from '@/api-access/generated/sub-categories/sub-categories';
import { getApiStatsSubCategoryMetrics } from '@/api-access/generated/sub-category-metrics/sub-category-metrics';
import { getApiUsers } from '@/api-access/generated/users/users';
import { useAccessControl } from '@/composables/useAccessControl';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaCard from '@/shared/components/UaCard.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import { useAuthStore } from '@/stores/auth';
import { useLocationsStore } from '@/stores/LocationsStore';
import type { SelectValue } from '@/types/select';
import { mdiChevronLeft, mdiChevronRight } from '@mdi/js';
import { DateTime } from 'luxon';
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import DayDetailPanel from '../components/DayDetailPanel.vue';
import WeeklyGrid from '../components/WeeklyGrid.vue';
import { getMondayOfWeek, useWeeklyRecords } from '../composables/useWeeklyRecords';
import { DAILY_REGULAR_TARGET_HOURS, EntryStatus } from '../constants';
import type { DayAssignment } from '../types';

const props = defineProps<{
  /** 1 = Non-Supervision, 2 = Supervision. Locks all assignments to that group. */
  groupId: number;
}>();

const GROUP_HEADER_COLORS: Record<number, string> = {
  1: '#42814A',
  2: '#CE3E39',
};
const cardHeaderColor = computed<string | undefined>(() => GROUP_HEADER_COLORS[props.groupId]);
const formTitle = computed(() => {
  if (props.groupId === 1) return 'Enter Non-Supervision Hours';
  if (props.groupId === 2) return 'Enter Supervision Hours';
  return 'Enter Hours Worked';
});

// ── Auth ──────────────────────────────────────────────────────────────────
const authStore = useAuthStore();
const { hasPermission } = useAccessControl();
const canEnterForOthers = computed(() => hasPermission(Permissions.StatsRecordsEnterForOthers));

// ── Deep-link query params (optional pre-seed from dashboard edit) ─────────
const route = useRoute();
const seedUserId = route.query.userId as string | undefined;
const parsedLocationId = Number(route.query.locationId);
const seedLocationId = Number.isFinite(parsedLocationId) ? parsedLocationId : undefined;
const rawDate = route.query.date as string | undefined;
const seedDate = rawDate && DateTime.fromISO(rawDate).isValid ? rawDate : undefined;

// ── Reference data ────────────────────────────────────────────────────────
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

  // When deep-linking with a pre-seeded location, load users so the Employee
  // dropdown shows names instead of raw IDs. Don't reset selectedUserId —
  // it is already seeded from the query param.
  if (seedLocationId) {
    const { data } = await getApiUsers({ LocationId: seedLocationId, IsEnabled: true });
    locationUsers.value = data.value ?? [];
  }

  isLoadingReference.value = false;

  if (seedDate && seedLocationId && seedUserId) {
    // Reference data is now populated, so reconstructAssignments will have
    // the lookups it needs. Load the week then select the day.
    await loadWeek();
    onSelectDay(seedDate);
  }
});

// ── Location ──────────────────────────────────────────────────────────────
const locationsStore = useLocationsStore();
const locationOptions = computed(() => locationsStore.getSelectOptions());
const selectedLocationId = ref<number | null>(seedLocationId ?? null);

const onLocationChange = (value: SelectValue | undefined) => {
  selectedLocationId.value = value != null ? Number(value) : null;
};

// ── Supervisor user picker ────────────────────────────────────────────────
const locationUsers = ref<UserResponse[]>([]);
const selectedUserId = ref<string | null>(seedUserId ?? (canEnterForOthers.value ? null : authStore.currentUserId));
let activeLocationId: number | null = null;

const userOptions = computed(() =>
  locationUsers.value.map((u) => ({ code: u.id, description: `${u.firstName} ${u.lastName}` })),
);

async function loadUsersForLocation(locationId: number) {
  activeLocationId = locationId;
  const { data } = await getApiUsers({ LocationId: locationId, IsEnabled: true });
  if (activeLocationId !== locationId) return;
  locationUsers.value = data.value ?? [];
  const currentAtLocation = locationUsers.value.some((u) => u.id === authStore.currentUserId);
  selectedUserId.value = currentAtLocation ? (authStore.currentUserId ?? null) : null;
}

watch(selectedLocationId, async (locId) => {
  if (canEnterForOthers.value && locId) {
    locationUsers.value = [];
    selectedUserId.value = null;
    await loadUsersForLocation(locId);
    // userId change triggers loadWeek reactively via useWeeklyRecords
  } else {
    selectedUserId.value = authStore.currentUserId;
    // locationId change triggers loadWeek reactively via useWeeklyRecords
  }
});

// ── Week navigation ───────────────────────────────────────────────────────
const weekRangeLabel = computed(() => {
  const dates = weekDates.value;
  if (dates.length < 7) return '';
  const from = DateTime.fromISO(dates[0]);
  const to = DateTime.fromISO(dates[6]);
  return `${from.toFormat('MMM d')} – ${to.toFormat('MMM d')}, ${to.year}`;
});

// ── Weekly records composable ─────────────────────────────────────────────
const {
  weekDates,
  dayAssignmentsMap,
  dayStatusMap,
  daySummaryMap,
  weeklyRegularTotal,
  weeklyOvertimeTotal,
  isOvertimeEnabled,
  isLoading,
  error: loadError,
  loadWeek,
  saveDay,
  navigateWeek,
  createEmptyAssignment,
} = useWeeklyRecords(
  getMondayOfWeek(seedDate ? DateTime.fromISO(seedDate) : DateTime.now()),
  selectedLocationId,
  selectedUserId,
  subCategories,
  categories,
  subCategoryMetrics,
  metrics,
);

// ── Selected day ──────────────────────────────────────────────────────────
const selectedDate = ref<string | null>(null);

const selectedAssignments = computed({
  get: () => {
    if (!selectedDate.value) return [];
    return dayAssignmentsMap.value[selectedDate.value] ?? [];
  },
  set: (val: DayAssignment[]) => {
    if (selectedDate.value) dayAssignmentsMap.value[selectedDate.value] = val;
  },
});

function onSelectDay(date: string) {
  selectedDate.value = date;
  // Ensure at least one empty assignment row for the selected day
  if (!dayAssignmentsMap.value[date] || dayAssignmentsMap.value[date].length === 0) {
    dayAssignmentsMap.value[date] = [createEmptyAssignment(props.groupId)];
  }
  dayErrors.value = {};
  apiError.value = '';
}

// ── Day detail mutations ──────────────────────────────────────────────────
function addAssignment() {
  if (!selectedDate.value) return;
  dayAssignmentsMap.value[selectedDate.value] = [...selectedAssignments.value, createEmptyAssignment(props.groupId)];
}

function removeAssignment(id: string) {
  if (!selectedDate.value) return;
  const remaining = selectedAssignments.value.filter((a) => a.id !== id);
  dayAssignmentsMap.value[selectedDate.value] =
    remaining.length > 0 ? remaining : [createEmptyAssignment(props.groupId)];
}

function updateAssignment(updated: DayAssignment) {
  if (!selectedDate.value) return;
  dayAssignmentsMap.value[selectedDate.value] = selectedAssignments.value.map((a) =>
    a.id === updated.id ? updated : a,
  );
}

// ── Validation ────────────────────────────────────────────────────────────
const dayErrors = ref<Record<string, string>>({});
const apiError = ref('');
const isSaving = ref(false);

function validate(assignments: DayAssignment[]): boolean {
  const errors: Record<string, string> = {};

  let dayTotalHours = 0;

  for (const [i, assignment] of assignments.entries()) {
    if (!assignment.categoryId) {
      errors[`assignment_${i}_category`] = 'Work Area is required';
    }
    if (!assignment.subCategoryId) {
      errors[`assignment_${i}_subCategory`] = 'Subcategory is required';
    }

    const scms = subCategoryMetrics.value.filter((scm) => scm.subCategoryId === assignment.subCategoryId);
    let hasValue = false;

    for (const scm of scms) {
      if (!scm.id) continue;
      const raw = assignment.metricValues[scm.id];
      if (!raw || raw.trim() === '') continue;

      const val = parseFloat(raw);
      if (isNaN(val)) {
        errors[`assignment_${i}_metric_${scm.id}`] = 'Must be a valid number';
        continue;
      }

      hasValue = true;
      const metric = metrics.value.find((m) => m.id === scm.metricId);
      const isRegular = metric?.unitOfMeasure === 'hours' && !metric.name?.toLowerCase().includes('overtime');

      if (isRegular && val > DAILY_REGULAR_TARGET_HOURS) {
        errors[`assignment_${i}_metric_${scm.id}`] =
          `Regular hours cannot exceed ${DAILY_REGULAR_TARGET_HOURS} per day`;
      }

      if (metric?.unitOfMeasure === 'hours') dayTotalHours += val;
    }

    if (!hasValue && assignment.subCategoryId) {
      errors[`assignment_${i}`] = 'Enter at least one metric value';
    }
  }

  if (dayTotalHours > 24) {
    errors['day'] = `Total hours (${dayTotalHours}h) cannot exceed 24h per day`;
  }

  dayErrors.value = errors;
  return Object.keys(errors).length === 0;
}

async function handleSave(status: string) {
  if (!selectedDate.value) return;
  if (!validate(selectedAssignments.value)) return;

  isSaving.value = true;
  apiError.value = '';
  try {
    const err = await saveDay(selectedDate.value, selectedAssignments.value, status);
    if (err) apiError.value = err;
  } finally {
    isSaving.value = false;
  }
}
</script>

<template>
  <div class="enter-hours-page">
    <!-- Page header -->
    <UaCard :header-color="cardHeaderColor">
      <template #header>
        <span class="ua-card__title">{{ formTitle }}</span>
      </template>

      <div class="page-header">
        <!-- Location selector -->
        <div class="header-field">
          <label class="field-label">Location</label>
          <UaSelect
            label="Select Location"
            :items="locationOptions"
            :model-value="selectedLocationId"
            :disabled="!!seedLocationId"
            @update:model-value="onLocationChange"
          />
        </div>

        <!-- Supervisor user picker -->
        <div v-if="canEnterForOthers" class="header-field">
          <label class="field-label">Employee</label>
          <UaSelect
            label="Select Employee"
            :items="userOptions"
            :model-value="selectedUserId"
            :disabled="!!seedUserId || !selectedLocationId"
            @update:model-value="(v) => (selectedUserId = v ? String(v) : null)"
          />
        </div>

        <!-- Week navigation -->
        <div class="week-nav">
          <v-btn icon variant="text" size="small" :prepend-icon="mdiChevronLeft" @click="navigateWeek(-1)">
            <v-icon :icon="mdiChevronLeft" />
          </v-btn>
          <span class="week-range">{{ weekRangeLabel }}</span>
          <v-btn icon variant="text" size="small" @click="navigateWeek(1)">
            <v-icon :icon="mdiChevronRight" />
          </v-btn>
        </div>
      </div>
    </UaCard>

    <!-- Loading state -->
    <div v-if="isLoadingReference" class="loading-state">Loading reference data…</div>

    <!-- Week load error -->
    <UaAlert v-if="loadError" type="error">{{ loadError }}</UaAlert>

    <template v-else>
      <!-- Main two-panel layout -->
      <div class="main-layout">
        <!-- Left: Weekly grid -->
        <div class="panel panel--grid">
          <div v-if="isLoading" class="loading-overlay">Loading…</div>
          <WeeklyGrid
            :week-dates="weekDates"
            :selected-date="selectedDate"
            :day-summary-map="daySummaryMap"
            :weekly-regular-total="weeklyRegularTotal"
            :weekly-overtime-total="weeklyOvertimeTotal"
            @select-day="onSelectDay"
          />
        </div>

        <!-- Right: Day detail panel -->
        <div class="panel panel--detail">
          <div v-if="!selectedDate" class="no-day-selected">
            <p>Select a day to enter hours</p>
          </div>
          <div v-else-if="!selectedLocationId" class="no-day-selected">
            <p>Select a location first</p>
          </div>
          <DayDetailPanel
            v-else
            :date="selectedDate"
            :group-id="groupId"
            :assignments="selectedAssignments"
            :groups="groups"
            :categories="categories"
            :sub-categories="subCategories"
            :sub-category-metrics="subCategoryMetrics"
            :metrics="metrics"
            :overtime-enabled="isOvertimeEnabled(selectedDate)"
            :is-saving="isSaving"
            :errors="dayErrors"
            :api-error="apiError"
            :header-color="cardHeaderColor"
            :day-status="dayStatusMap[selectedDate]"
            @add-assignment="addAssignment"
            @remove-assignment="removeAssignment"
            @update-assignment="updateAssignment"
            @save-draft="handleSave(EntryStatus.Draft)"
            @submit-day="handleSave(EntryStatus.Submitted)"
            @clear-error="apiError = ''"
          />
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.enter-hours-page {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-lg);
  padding: var(--ua-spacing-xl);
  max-width: var(--ua-content-max-width);
}

.page-header {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-lg);
  align-items: flex-end;
}

.header-field {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xs);
  min-width: 220px;
  flex: 1;
}

.field-label {
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-secondary);
}

.week-nav {
  display: flex;
  align-items: center;
  gap: var(--ua-spacing-xs);
  margin-left: auto;
}

.week-range {
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-semibold);
  color: var(--ua-text-primary);
  min-width: 200px;
  text-align: center;
}

.loading-state,
.loading-overlay {
  padding: var(--ua-spacing-md);
  color: var(--ua-text-muted);
  font-size: var(--ua-font-size-sm);
}

.loading-overlay {
  position: absolute;
  inset: 0;
  background: rgba(255, 255, 255, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1;
  border-radius: var(--ua-border-radius);
}

.main-layout {
  display: grid;
  grid-template-columns: minmax(320px, 2fr) minmax(400px, 3fr);
  gap: var(--ua-spacing-xl);
  align-items: start;
}

.panel {
  position: relative;
}

.no-day-selected {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 200px;
  border: 2px dashed var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  color: var(--ua-text-muted);
  font-size: var(--ua-font-size-sm);
}

/* Mobile: stack panels */
@media (max-width: 900px) {
  .main-layout {
    grid-template-columns: 1fr;
  }

  .panel--grid {
    position: sticky;
    top: 0;
    background: rgb(var(--v-theme-background));
    z-index: 5;
    padding-bottom: var(--ua-spacing-md);
  }

  .enter-hours-page {
    padding: var(--ua-spacing-md);
  }
}
</style>
