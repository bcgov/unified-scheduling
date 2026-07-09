<script setup lang="ts">
import UaAlert from '@/shared/components/UaAlert.vue';
import UaCard from '@/shared/components/UaCard.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import { mdiChevronLeft, mdiChevronRight } from '@mdi/js';
import { computed } from 'vue';
import DayDetailPanel from '../components/DayDetailPanel.vue';
import WeeklyGrid from '../components/WeeklyGrid.vue';
import { useEnterHours } from '../composables/useEnterHours';
import { EntryStatus } from '../constants';

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

const {
  accountWarning,
  canEnterForOthers,
  seedLocationId,
  seedUserId,
  isLoadingReference,
  groups,
  categories,
  subCategories,
  metrics,
  subCategoryMetrics,
  locationOptions,
  selectedLocationId,
  onLocationChange,
  selectedUserId,
  userOptions,
  weekDates,
  dayStatusMap,
  daySummaryMap,
  weeklyRegularTotal,
  weeklyOvertimeTotal,
  isOvertimeEnabled,
  isLoading,
  loadError,
  navigateWeek,
  weekRangeLabel,
  selectedDate,
  selectedAssignments,
  onSelectDay,
  addAssignment,
  removeAssignment,
  updateAssignment,
  copyFromOptions,
  copyFromDay,
  dayErrors,
  apiError,
  isSaving,
  handleSave,
} = useEnterHours(props.groupId);
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

        <!-- Employee picker (supervisors only) -->
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

    <!-- Account setup warning -->
    <UaAlert v-if="accountWarning" type="warning">{{ accountWarning }}</UaAlert>

    <!-- Loading state -->
    <div v-if="isLoadingReference && !accountWarning" class="loading-state">Loading reference data…</div>

    <!-- Week load error -->
    <UaAlert v-if="loadError" type="error">{{ loadError }}</UaAlert>

    <template v-else-if="!accountWarning">
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
            :copy-from-options="copyFromOptions"
            @add-assignment="addAssignment"
            @remove-assignment="removeAssignment"
            @update-assignment="updateAssignment"
            @copy-from="copyFromDay"
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
