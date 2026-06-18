<script setup lang="ts">
import type {
  StatCategoryResponse,
  StatGroupResponse,
  StatMetricResponse,
  SubCategoryMetricResponse,
  SubCategoryResponse,
} from '@/api-access/generated/models';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import { mdiLockOutline, mdiPlus } from '@mdi/js';
import { computed } from 'vue';
import type { DayAssignment } from '../types';
import { DAILY_REGULAR_TARGET_HOURS, WEEKLY_REGULAR_TARGET_HOURS } from '../constants';
import { isRegularMetric } from '../utils/metricHelpers';
import AssignmentRow from './AssignmentRow.vue';

const props = defineProps<{
  date: string;
  groupId: number;
  assignments: DayAssignment[];
  groups: StatGroupResponse[];
  categories: StatCategoryResponse[];
  subCategories: SubCategoryResponse[];
  subCategoryMetrics: SubCategoryMetricResponse[];
  metrics: StatMetricResponse[];
  overtimeEnabled: boolean;
  isSaving: boolean;
  errors: Record<string, string>;
  apiError: string;
  headerColor?: string;
}>();

const emit = defineEmits<{
  'add-assignment': [];
  'remove-assignment': [id: string];
  'update-assignment': [assignment: DayAssignment];
  'save-draft': [];
  'submit-day': [];
  'clear-error': [];
}>();

const formattedDate = computed(() => {
  const d = new Date(props.date + 'T00:00:00');
  return d.toLocaleDateString('en-CA', { weekday: 'long', month: 'long', day: 'numeric' });
});

const dailyRegularTotal = computed(() => {
  let total = 0;
  for (const a of props.assignments) {
    for (const [scmIdStr, valStr] of Object.entries(a.metricValues)) {
      const val = parseFloat(valStr);
      if (isNaN(val) || val <= 0) continue;
      const scm = props.subCategoryMetrics.find((s) => s.id === Number(scmIdStr));
      const metric = props.metrics.find((m) => m.id === scm?.metricId);
      if (metric && isRegularMetric(metric)) {
        total += val;
      }
    }
  }
  return total;
});

const overtimeLockReason = computed(() => {
  if (props.overtimeEnabled) return '';
  return `Enter ${DAILY_REGULAR_TARGET_HOURS}h regular today (${dailyRegularTotal.value}h / ${DAILY_REGULAR_TARGET_HOURS}h) or ${WEEKLY_REGULAR_TARGET_HOURS}h for the week to unlock overtime.`;
});
</script>

<template>
  <div class="day-detail-panel">
    <!-- Header -->
    <div class="day-detail-panel__header" :style="headerColor ? { borderLeftColor: headerColor } : {}">
      <div>
        <h2 class="day-detail-panel__date">{{ formattedDate }}</h2>
        <p class="day-detail-panel__total">
          Regular: <strong>{{ dailyRegularTotal }}h</strong> / {{ DAILY_REGULAR_TARGET_HOURS }}h
        </p>
      </div>
      <div v-if="!overtimeEnabled" class="overtime-locked-badge">
        <v-icon :icon="mdiLockOutline" size="14" />
        Overtime locked
      </div>
    </div>

    <!-- Alerts -->
    <UaAlert v-if="apiError" type="error" @close="emit('clear-error')">
      {{ apiError }}
    </UaAlert>
    <UaAlert v-if="errors['day']" type="error">{{ errors['day'] }}</UaAlert>

    <!-- Assignment rows -->
    <div class="day-detail-panel__assignments">
      <AssignmentRow
        v-for="(assignment, i) in assignments"
        :key="assignment.id"
        :model-value="assignment"
        :groups="groups"
        :categories="categories"
        :sub-categories="subCategories"
        :sub-category-metrics="subCategoryMetrics"
        :metrics="metrics"
        :index="i"
        :errors="errors"
        :fixed-group-id="groupId"
        :header-color="headerColor"
        :overtime-locked="!overtimeEnabled"
        :overtime-lock-reason="overtimeLockReason"
        @remove="emit('remove-assignment', assignment.id)"
        @update:model-value="(v) => emit('update-assignment', v as DayAssignment)"
      />

      <UaBtn variant="outlined" class="add-btn" :prepend-icon="mdiPlus" @click="emit('add-assignment')">
        Add Assignment
      </UaBtn>
    </div>

    <!-- Form-level errors -->
    <p v-if="errors['form']" class="form-error">{{ errors['form'] }}</p>

    <!-- Actions -->
    <div class="day-detail-panel__actions">
      <UaBtn variant="outlined" color="primary" :loading="isSaving" @click="emit('save-draft')"> Save Draft </UaBtn>
      <UaBtn color="primary" variant="flat" :loading="isSaving" @click="emit('submit-day')"> Submit Day </UaBtn>
    </div>
  </div>
</template>

<style scoped>
.day-detail-panel {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-md);
  height: 100%;
}

.day-detail-panel__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  padding-bottom: var(--ua-spacing-md);
  border-bottom: 2px solid var(--ua-border-color);
  border-left: 4px solid var(--ua-border-color);
  padding-left: var(--ua-spacing-md);
}

.day-detail-panel__date {
  font-size: var(--ua-font-size-xl);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
  margin: 0 0 4px;
}

.day-detail-panel__total {
  font-size: var(--ua-font-size-sm);
  color: var(--ua-text-secondary);
  margin: 0;
}

.overtime-locked-badge {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: var(--ua-font-size-xs);
  color: var(--ua-text-muted);
  background: var(--ua-card-body-bg);
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  padding: 4px 8px;
  white-space: nowrap;
}

.day-detail-panel__assignments {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-md);
  flex: 1;
  overflow-y: auto;
}

.add-btn {
  align-self: flex-start;
}

.form-error {
  font-size: var(--ua-font-size-sm);
  color: rgb(var(--v-theme-error));
  margin: 0;
}

.day-detail-panel__actions {
  display: flex;
  gap: var(--ua-spacing-md);
  justify-content: flex-end;
  padding-top: var(--ua-spacing-sm);
  border-top: 1px solid var(--ua-border-color);
}
</style>
