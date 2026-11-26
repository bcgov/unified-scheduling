<script setup lang="ts">
import { mdiAlert, mdiAlertCircle, mdiChevronDown, mdiChevronUp } from '@mdi/js';
import { computed, onMounted, reactive, ref } from 'vue';
import type {
  CalendarRestrictedConflictWarningResponse,
  SchedulePublishConflictResponse,
  SchedulePublishPreviewResponse,
  SchedulePublishRequest,
  SchedulePublishShiftEventResponse,
} from '@/api-access/generated/models';
import {
  postApiSchedulingPublish,
  postApiSchedulingPublishPreview,
} from '@/api-access/generated/schedule-publish/schedule-publish';
import { PostApiSchedulingPublishPreviewResponse } from '@/api-access/generated/schedule-publish/schedule-publish.zod';
import { formatCalendarConflictEventRange } from '@/modules/calendar/calendarConflictFormatting';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import { formatCalendarDateTimeDate, formatCalendarDateTimeRange, formatCalendarTime } from '@/utils/date';

const props = defineProps<{
  request: SchedulePublishRequest;
}>();

const emit = defineEmits<{
  (event: 'close'): void;
  (event: 'published', count: number): void;
}>();

const locationsStore = useLocationsStore();
const locationName = computed(() => locationsStore.entitiesMap[props.request.locationId]?.name || 'Unknown location');
const preview = ref<SchedulePublishPreviewResponse>();
const isLoading = ref(false);
const isPublishing = ref(false);
const errorMessage = ref('');
const previewUpdated = ref(false);
const collapsedEmployeeIds = reactive(new Set<string>());
const hasNoCandidates = computed(
  () => preview.value?.candidates.shiftEntryCount === 0 && preview.value.candidates.assignmentEntryCount === 0,
);

const severity = {
  error: { icon: mdiAlertCircle, label: 'Error (Blocking)' },
  warning: { icon: mdiAlert, label: 'Warning (Non-blocking)' },
};

type EmployeeFinding = {
  key: string;
  tone: keyof typeof severity;
  message: string;
  details?: string[];
};

type EmployeeGroup = { userId: string; displayName: string; findings: EmployeeFinding[] };

const employeeGroups = computed(() => {
  const current = preview.value;
  if (!current) return [];

  const { warnings, blockers, scope } = current;
  const names = new Map((current.affectedEmployees ?? []).map(({ userId, displayName }) => [userId, displayName]));
  for (const employee of warnings.incompleteEmployees) {
    if (!names.has(employee.userId)) names.set(employee.userId, employee.displayName);
  }

  const groups = new Map<string, EmployeeGroup>();
  function addFinding(userId: string, finding: EmployeeFinding) {
    let group = groups.get(userId);
    if (!group) {
      group = { userId, displayName: names.get(userId) || 'Unknown employee', findings: [] };
      groups.set(userId, group);
    }
    group.findings.push(finding);
  }

  function addConflict(conflict: SchedulePublishConflictResponse, tone: EmployeeFinding['tone']) {
    const details = [conflict.entry, conflict.overlaps].flatMap((event) =>
      event ? [`${event.title} · ${formatCalendarConflictEventRange(event, scope.timeZoneId)}`] : [],
    );
    addFinding(conflict.resourceId, {
      key: conflict.id,
      tone,
      message: conflict.title || 'Unresolved calendar conflict',
      details: [
        ...details,
        `Conflict overlap · ${formatCalendarDateTimeRange(conflict.overlapStart, conflict.overlapEnd, scope.timeZoneId)}`,
      ],
    });
  }

  function addRestrictedFinding(warning: CalendarRestrictedConflictWarningResponse, tone: EmployeeFinding['tone']) {
    const event = warning.authorizedEvent;
    addFinding(warning.resourceId, {
      key: `restricted:${warning.sourceModule}:${warning.eventId}`,
      tone,
      message: warning.message,
      details: event ? [`${event.title} · ${formatCalendarConflictEventRange(event, scope.timeZoneId)}`] : undefined,
    });
  }

  for (const conflict of blockers.conflicts) addConflict(conflict, 'error');
  for (const blocker of blockers.shiftConflicts) {
    if (!blocker.userId) continue;
    addFinding(blocker.userId, {
      key: `shift:${blocker.firstShift?.shiftEntryId}:${blocker.secondShift?.shiftEntryId}`,
      tone: 'error',
      message: blocker.message,
      details: [blocker.firstShift, blocker.secondShift].flatMap((shift) =>
        shift ? [`${shift.title || 'Shift'} · ${formatShiftRange(shift, scope.timeZoneId)}`] : [],
      ),
    });
  }

  for (const warning of blockers.restrictedWarnings ?? []) addRestrictedFinding(warning, 'error');
  for (const conflict of warnings.conflicts ?? []) addConflict(conflict, 'warning');
  for (const warning of warnings.restrictedWarnings ?? []) addRestrictedFinding(warning, 'warning');

  for (const employee of warnings.incompleteEmployees) {
    const missing = [employee.missingShift && 'a shift', employee.missingAssignment && 'an assignment'].filter(Boolean);
    const message = `Missing ${missing.join(' and ') || 'a shift or an assignment'}.`;
    addFinding(employee.userId, { key: 'incomplete', tone: 'warning', message });
  }

  return [...groups.values()].sort(
    (first, second) =>
      Number(first.findings[0]?.tone === 'warning') - Number(second.findings[0]?.tone === 'warning') ||
      first.displayName.localeCompare(second.displayName, 'en-CA') ||
      first.userId.localeCompare(second.userId),
  );
});

const generalFindings = computed(() => {
  const current = preview.value;
  if (!current) return [];

  const { blockers, warnings } = current;
  const findings: { key: string; tone: 'error' | 'warning' | 'info'; message: string }[] = [];
  const hasUnattributed =
    blockers.hasUnattributedRestrictedConflicts ??
    (blockers.hasRestrictedConflicts && !blockers.restrictedWarnings?.length);
  if (hasUnattributed) {
    findings.push({
      key: 'restricted-blocker',
      tone: 'error',
      message: 'Unresolved conflicts include events you do not have permission to view.',
    });
  }
  blockers.shiftConflicts.forEach((blocker, index) => {
    if (!blocker.userId) findings.push({ key: `shift:${index}`, tone: 'error', message: blocker.message });
  });
  if (warnings.hasUnattributedRestrictedConflicts) {
    findings.push({
      key: 'restricted-warning',
      tone: 'warning',
      message: 'Unresolved conflicts on published schedule items include events you do not have permission to view.',
    });
  }
  const shifts = warnings.unassignedShifts.length;
  const assignments = warnings.unassignedAssignments.length;
  if (shifts) {
    findings.push({
      key: 'shifts',
      tone: 'warning',
      message: shifts === 1 ? '1 shift is unassigned.' : `${shifts} shifts are unassigned.`,
    });
  }
  if (assignments) {
    findings.push({
      key: 'assignments',
      tone: 'warning',
      message: assignments === 1 ? '1 assignment is unassigned.' : `${assignments} assignments are unassigned.`,
    });
  }
  if (hasNoCandidates.value) {
    findings.push({
      key: 'no-candidates',
      tone: 'info',
      message: 'No draft schedule items are available to publish for this location and date range.',
    });
  }
  return findings;
});

const publicationStatus = computed(() => {
  if (!preview.value) return '';
  if (hasNoCandidates.value) return 'No draft items to publish.';
  if (!preview.value.canPublish) return 'Resolve the blocking errors before publishing.';
  if (employeeGroups.value.length || generalFindings.value.length)
    return 'You can publish this schedule with warnings.';
  return 'This schedule is ready to publish.';
});

onMounted(loadPreview);

function toggleEmployeeFindings(userId: string) {
  if (collapsedEmployeeIds.has(userId)) {
    collapsedEmployeeIds.delete(userId);
  } else {
    collapsedEmployeeIds.add(userId);
  }
}

async function loadPreview() {
  isLoading.value = true;
  errorMessage.value = '';

  try {
    const { data, error, execute } = postApiSchedulingPublishPreview(props.request, {
      options: { immediate: false },
    });
    await execute();

    if (error.value || !data.value) {
      errorMessage.value = error.value?.message || 'Failed to preview this schedule publication.';
      return;
    }

    preview.value = data.value;
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : 'Failed to preview this schedule publication.';
  } finally {
    isLoading.value = false;
  }
}

async function publishSchedule() {
  if (isLoading.value || isPublishing.value || !preview.value?.canPublish) {
    return;
  }

  isPublishing.value = true;
  errorMessage.value = '';
  previewUpdated.value = false;

  try {
    const { data, error, statusCode, execute } = postApiSchedulingPublish(props.request, {
      options: { immediate: false },
    });
    await execute();

    if (statusCode.value === 409) {
      const blockingPreview = PostApiSchedulingPublishPreviewResponse.safeParse(data.value);
      if (blockingPreview.success) {
        preview.value = blockingPreview.data;
        previewUpdated.value = true;
        return;
      }
    }

    if (error.value || !data.value) {
      errorMessage.value = error.value?.message || 'Failed to publish this schedule.';
      return;
    }

    const publishedCount = data.value.publishedShiftEntryCount + data.value.publishedAssignmentEntryCount;
    emit('published', publishedCount);
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : 'Failed to publish this schedule.';
  } finally {
    isPublishing.value = false;
  }
}

function formatShiftRange(shift: SchedulePublishShiftEventResponse, timeZone: string) {
  if (shift.endAtUtc) return formatCalendarDateTimeRange(shift.startAtUtc, shift.endAtUtc, timeZone);
  return `${formatCalendarDateTimeDate(shift.startAtUtc, timeZone)}, ${formatCalendarTime(shift.startAtUtc, timeZone)}`;
}

function toAvatarText(displayName: string) {
  return displayName
    .split(/\s+/)
    .filter(Boolean)
    .map((part) => part.charAt(0))
    .join('')
    .slice(0, 2)
    .toUpperCase();
}
</script>

<template>
  <UaModal title="Publish Schedule" width="680" :loading="isLoading || isPublishing" @close="emit('close')">
    <template #alerts>
      <UaAlert v-if="errorMessage" type="error" :closable="false">{{ errorMessage }}</UaAlert>
      <UaAlert v-else-if="previewUpdated" type="info" :closable="false">
        The schedule changed. Review the updated publication findings.
      </UaAlert>
    </template>

    <template #secondary-header>
      <dl class="schedule-publish__scope">
        <div>
          <dt>Location</dt>
          <dd>{{ locationName }}</dd>
        </div>
        <div>
          <dt>Date range</dt>
          <dd>{{ request.startDate }} to {{ request.endDate }}</dd>
        </div>
      </dl>

      <ul class="schedule-publish__severity-key" role="list" aria-label="Publication severity key">
        <li class="schedule-publish__finding">
          <v-icon :icon="severity.error.icon" color="error" size="18" aria-hidden="true" />
          <span
            ><strong>{{ severity.error.label }}</strong> · Must be resolved before publishing this schedule.</span
          >
        </li>
        <li class="schedule-publish__finding">
          <v-icon :icon="severity.warning.icon" color="warning" size="18" aria-hidden="true" />
          <span
            ><strong>{{ severity.warning.label }}</strong> · Requires attention. You can publish this schedule with
            warnings.</span
          >
        </li>
      </ul>
    </template>

    <div v-if="isLoading" class="schedule-publish__state">Checking this schedule...</div>

    <div v-else-if="preview" class="schedule-publish">
      <section v-if="employeeGroups.length" class="schedule-publish__section">
        <h2 class="schedule-publish__section-title">Employee-specific issues</h2>
        <ul class="schedule-publish__employee-list" role="list">
          <li v-for="employee in employeeGroups" :key="employee.userId" class="schedule-publish__employee">
            <span class="schedule-publish__employee-avatar" aria-hidden="true">
              {{ toAvatarText(employee.displayName) }}
            </span>
            <div class="schedule-publish__employee-content">
              <div class="schedule-publish__employee-header">
                <h3 class="schedule-publish__employee-name">{{ employee.displayName }}</h3>
                <UaBtn
                  icon
                  type="button"
                  size="small"
                  variant="text"
                  :aria-label="`Findings for ${employee.displayName}`"
                  :aria-expanded="!collapsedEmployeeIds.has(employee.userId)"
                  @click="toggleEmployeeFindings(employee.userId)"
                >
                  <v-icon
                    :icon="collapsedEmployeeIds.has(employee.userId) ? mdiChevronDown : mdiChevronUp"
                    aria-hidden="true"
                  />
                </UaBtn>
              </div>
              <ul
                v-if="!collapsedEmployeeIds.has(employee.userId)"
                class="schedule-publish__employee-findings"
                role="list"
              >
                <li v-for="finding in employee.findings" :key="finding.key" class="schedule-publish__finding">
                  <v-icon :icon="severity[finding.tone].icon" :color="finding.tone" size="18" aria-hidden="true" />
                  <div>
                    <span class="schedule-publish__sr-only">{{ severity[finding.tone].label }}. </span>
                    {{ finding.message }}
                    <div v-for="(detail, index) in finding.details" :key="index" class="schedule-publish__detail">
                      {{ detail }}
                    </div>
                  </div>
                </li>
              </ul>
            </div>
          </li>
        </ul>
      </section>

      <section v-if="generalFindings.length" class="schedule-publish__section">
        <h2 class="schedule-publish__section-title">General schedule issues</h2>
        <ul class="schedule-publish__general-findings" role="list">
          <li v-for="finding in generalFindings" :key="finding.key">
            <UaAlert
              :type="finding.tone"
              :icon="finding.tone === 'info' ? undefined : severity[finding.tone].icon"
              :closable="false"
            >
              <span v-if="finding.tone !== 'info'" class="schedule-publish__sr-only">
                {{ severity[finding.tone].label }}.
              </span>
              {{ finding.message }}
            </UaAlert>
          </li>
        </ul>
      </section>
    </div>

    <template #actions>
      <div v-show="publicationStatus" class="schedule-publish__status" role="status">{{ publicationStatus }}</div>
      <UaBtn variant="outlined" :disabled="isPublishing" @click="emit('close')">Cancel</UaBtn>
      <UaBtn
        color="primary"
        variant="flat"
        :loading="isPublishing"
        :disabled="isLoading || isPublishing || !preview?.canPublish"
        @click="publishSchedule"
      >
        Publish Schedule
      </UaBtn>
    </template>
  </UaModal>
</template>

<style scoped>
.schedule-publish__scope {
  display: grid;
  gap: var(--ua-spacing-sm);
  margin: 0;
}

.schedule-publish__scope div {
  display: grid;
  grid-template-columns: 7rem 1fr;
}

.schedule-publish__scope dt {
  font-weight: var(--ua-font-weight-semibold);
}

.schedule-publish__scope dd {
  margin: 0;
}

.schedule-publish {
  display: grid;
  gap: var(--ua-spacing-lg);
}

.schedule-publish__severity-key,
.schedule-publish__employee-list,
.schedule-publish__employee-findings,
.schedule-publish__general-findings {
  list-style: none;
  margin: 0;
  padding: 0;
}

.schedule-publish__severity-key,
.schedule-publish__employee-findings,
.schedule-publish__general-findings,
.schedule-publish__section {
  display: grid;
  gap: var(--ua-spacing-sm);
}

.schedule-publish__severity-key {
  margin-top: var(--ua-spacing-sm);
}

.schedule-publish__finding {
  display: flex;
  align-items: flex-start;
  gap: var(--ua-spacing-xs);
  font-size: var(--ua-font-size-sm);
  overflow-wrap: anywhere;
}

.schedule-publish__finding > .v-icon {
  margin-block-start: calc(var(--ua-spacing-xs) / 2);
}

.schedule-publish__section-title {
  font-size: var(--ua-font-size-md);
  font-weight: var(--ua-font-weight-semibold);
  margin: 0;
}

.schedule-publish__employee-list {
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius-sm);
  overflow: hidden;
}

.schedule-publish__employee {
  align-items: flex-start;
  background: rgb(var(--v-theme-surface));
  display: flex;
  gap: var(--ua-spacing-sm);
  min-width: 0;
  padding: var(--ua-spacing-sm) var(--ua-spacing-md);
}

.schedule-publish__employee + .schedule-publish__employee {
  border-top: 1px solid var(--ua-border-color);
}

.schedule-publish__employee-avatar {
  align-items: center;
  background: rgb(var(--v-theme-surface-variant));
  border: 1px solid var(--ua-border-color);
  border-radius: 50%;
  color: var(--ua-text-primary);
  display: inline-flex;
  flex: 0 0 2rem;
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-bold);
  height: 2rem;
  justify-content: center;
  width: 2rem;
}

.schedule-publish__employee-content {
  display: grid;
  flex: 1;
  gap: var(--ua-spacing-xs);
  min-width: 0;
}

.schedule-publish__employee-header {
  align-items: center;
  display: flex;
  gap: var(--ua-spacing-sm);
  justify-content: space-between;
}

.schedule-publish__employee-name {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-bold);
  margin: 0;
  overflow-wrap: anywhere;
}

.schedule-publish__detail {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-xs);
}

.schedule-publish__status {
  grid-column: 1 / -1;
  font-size: var(--ua-font-size-sm);
}

.schedule-publish__sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}
</style>
