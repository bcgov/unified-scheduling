<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { ShiftEntryRequest } from '@/api-access/generated/models/shiftEntryRequest';
import type { ShiftEntryResponse } from '@/api-access/generated/models/shiftEntryResponse';
import type { ShiftSeriesRequest } from '@/api-access/generated/models/shiftSeriesRequest';
import type { ShiftSeriesResponse } from '@/api-access/generated/models/shiftSeriesResponse';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import RRuleEditor from '@/components/recurrence/RRuleEditor.vue';
import { isCalendarSchedulingEvent } from './calendarSchedulingData';
import CalendarSchedulingShiftForm from './CalendarSchedulingShiftForm.vue';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import { useLocationsStore } from '@/stores/LocationsStore';
import { mapToValidationErrors } from '@/shared/validation/validationErrors';
import {
  buildUpdateShiftPayload,
  buildShiftTitle,
  createShiftFormDataFromEntry,
  createShiftFormDataFromEvent,
  createShiftFormDataFromSeries,
  normalizeShiftFormTimes,
  validateShiftFormData,
  type ShiftResourceFormData,
} from './calendarSchedulingShiftForm';
import * as shiftApi from './calendarSchedulingShiftApi';
import { syncAssignmentEntryLinks } from './calendarSchedulingShiftAssignmentApi';
import { useSchedulingEmployeeOptions } from './useSchedulingEmployeeOptions';
import { useSchedulingAssignmentOptions } from './useSchedulingAssignmentOptions';
import { useSchedulingShiftDetailRows } from './useSchedulingShiftDetailRows';

type ShiftDetailTabId = 'details' | 'edit' | 'delete';
type ShiftOpenScope = 'event' | 'series';

const props = defineProps<{
  event: CalendarEventBase;
  initialOpenScope?: ShiftOpenScope;
}>();

const emit = defineEmits<{
  (event: 'close'): void;
}>();

const calendarStore = useCalendarStore();
const locationsStore = useLocationsStore();

const activeTab = ref<ShiftDetailTabId>('details');
const selectedOpenScope = ref<ShiftOpenScope | null>(getInitialOpenScope());
const isSaving = ref(false);
const apiError = ref('');
const isLoadingSeries = ref(false);
const formErrors = ref<Record<string, string>>({});
const recurrenceError = ref('');
const isDeleteConfirmed = ref(false);
const selectedEntry = ref<ShiftEntryResponse | null>(null);
const selectedSeries = ref<ShiftSeriesResponse | null>(null);
const timeZoneId = computed(() => props.event.timeZoneId || Intl.DateTimeFormat().resolvedOptions().timeZone);
const activeTimeZoneId = computed(() =>
  selectedOpenScope.value === 'series' ? selectedSeries.value?.timeZoneId || timeZoneId.value : timeZoneId.value,
);
const editFormData = ref<ShiftResourceFormData>(createEditFormData());
const activeLocationId = computed<number | null>(() => {
  if (selectedOpenScope.value === 'series' && selectedSeries.value?.locationId != null) {
    return selectedSeries.value.locationId;
  }

  if (props.event.locationId != null) {
    return props.event.locationId;
  }

  const candidate = locationsStore.selectedLocationId;

  if (candidate === '' || candidate == null) {
    return null;
  }

  const parsedLocationId = Number(candidate);
  return Number.isFinite(parsedLocationId) ? parsedLocationId : null;
});
const { employeeOptions, isLoadingUsers } = useSchedulingEmployeeOptions(activeLocationId, editFormData, {
  onError: (message) => {
    apiError.value = message;
  },
});
const locationOptions = computed(() => locationsStore.selectOptions);
const eventBelongsToSeries = computed(() => resolveShiftSeriesId() !== null);
const shouldShowOpenScopeChoice = computed(() => eventBelongsToSeries.value && selectedOpenScope.value === null);
const isSeriesScope = computed(() => selectedOpenScope.value === 'series');
const shiftEntityLabel = computed(() => (isSeriesScope.value ? 'Shift Series' : 'Shift'));
const currentStatusTypeCode = computed(() =>
  String(
    isSeriesScope.value
      ? selectedSeries.value?.statusTypeCode
      : (selectedEntry.value?.statusTypeCode ?? props.event.statusTypeCode),
  ).toLowerCase(),
);
const isDraftShift = computed(() => currentStatusTypeCode.value === 'draft');
const isActiveShift = computed(() => currentStatusTypeCode.value === 'active');
const tabs = computed<Array<{ id: ShiftDetailTabId; label: string }>>(() => [
  { id: 'details', label: 'Details' },
  ...(isDraftShift.value ? [{ id: 'edit' as const, label: 'Edit' }] : []),
  ...(isDraftShift.value || isActiveShift.value
    ? [{ id: 'delete' as const, label: isActiveShift.value ? 'Cancel' : 'Delete' }]
    : []),
]);
const modalTitle = computed(() => {
  if (shouldShowOpenScopeChoice.value) {
    return 'Open Shift';
  }

  if (activeTab.value === 'edit') {
    return `Edit ${shiftEntityLabel.value}`;
  }

  if (activeTab.value === 'delete') {
    return `${isActiveShift.value ? 'Cancel' : 'Delete'} ${shiftEntityLabel.value}`;
  }

  return `${shiftEntityLabel.value} Details`;
});
const deleteDisabledReason = computed(() => {
  if (currentStatusTypeCode.value && !isDraftShift.value && !isActiveShift.value) {
    return selectedOpenScope.value === 'series'
      ? 'Only draft or published shift series can be deleted.'
      : 'Only draft or published shift entries can be deleted.';
  }

  return '';
});
const canDeleteShift = computed(() => !deleteDisabledReason.value && isDeleteConfirmed.value);
const deleteConfirmationLabel = computed(() =>
  isActiveShift.value
    ? 'I understand this published shift will be cancelled.'
    : 'I understand this shift will be permanently deleted.',
);
const deleteWarning = computed(() =>
  isActiveShift.value ? 'This published shift will be cancelled.' : "This can't be undone.",
);
const { assignmentEntryOptions, assignmentSeriesOptions, assignmentWarning, isLoadingAssignments } =
  useSchedulingAssignmentOptions({
    formData: editFormData,
    activeLocationId,
    activeTimeZoneId,
    isSeriesScope,
    onError: (message) => {
      apiError.value = message;
    },
  });
const { detailRows } = useSchedulingShiftDetailRows({
  event: computed(() => props.event),
  selectedOpenScope,
  selectedSeries,
  formData: editFormData,
  employeeOptions,
  assignmentEntryOptions,
  assignmentSeriesOptions,
  locationOptions,
  activeTimeZoneId,
});

watch(
  () => props.event,
  async (event) => {
    selectedEntry.value = null;
    selectedSeries.value = null;
    editFormData.value = createShiftFormDataFromEvent(event, timeZoneId.value);
    activeTab.value = 'details';
    selectedOpenScope.value = getInitialOpenScope();
    apiError.value = '';
    formErrors.value = {};
    recurrenceError.value = '';
    isDeleteConfirmed.value = false;

    if (!resolveShiftSeriesId()) {
      await loadSelectedEntry();
    }
  },
  { immediate: true },
);

watch(
  () => editFormData.value.repeatMode,
  (value) => {
    if (value === 'never') {
      editFormData.value.recurrenceRule = null;
      recurrenceError.value = '';
    }
  },
);

function selectTab(tabId: ShiftDetailTabId) {
  if (!tabs.value.some((tab) => tab.id === tabId)) {
    return;
  }

  activeTab.value = tabId;
  apiError.value = '';
  isDeleteConfirmed.value = false;
}

async function selectOpenScope(scope: ShiftOpenScope) {
  apiError.value = '';

  if (scope === 'series') {
    const series = await loadSelectedSeries();
    if (!series) {
      return;
    }
  } else {
    selectedSeries.value = null;
    const entry = await loadSelectedEntry();
    if (!entry) {
      return;
    }
  }

  selectedOpenScope.value = scope;
  editFormData.value = createEditFormData();
  activeTab.value = 'details';
}

async function loadSelectedEntry() {
  const id = resolveShiftEntryId();
  if (!id) {
    apiError.value = 'Could not determine the shift entry to open.';
    return null;
  }

  const result = await shiftApi.loadShiftEntry(id);
  if (result.error.value) {
    apiError.value = result.error.value.message || 'Failed to load the shift entry.';
    return null;
  }

  selectedEntry.value = result.data.value ?? null;
  if (!selectedEntry.value) {
    apiError.value = 'Shift entry was not found.';
    return null;
  }

  editFormData.value = createShiftFormDataFromEntry(selectedEntry.value, props.event, activeTimeZoneId.value);
  return selectedEntry.value;
}

async function loadSelectedSeries() {
  const id = resolveShiftSeriesId();
  if (!id) {
    apiError.value = 'Could not determine the shift series to open.';
    return null;
  }

  isLoadingSeries.value = true;

  try {
    const result = await shiftApi.loadShiftSeries(id);

    if (result.error.value) {
      apiError.value = result.error.value.message || 'Failed to load shift series.';
      return null;
    }

    selectedSeries.value = result.data.value ?? null;
    if (!selectedSeries.value) {
      apiError.value = 'Shift series was not found.';
    }

    return selectedSeries.value;
  } finally {
    isLoadingSeries.value = false;
  }
}

function createEditFormData(): ShiftResourceFormData {
  if (selectedOpenScope.value === 'series' && selectedSeries.value) {
    return createShiftFormDataFromSeries(selectedSeries.value, props.event, activeTimeZoneId.value);
  }

  if (selectedEntry.value) {
    return createShiftFormDataFromEntry(selectedEntry.value, props.event, activeTimeZoneId.value);
  }

  return createShiftFormDataFromEvent(props.event, timeZoneId.value);
}

function handleRecurrenceInvalid(reason: string) {
  recurrenceError.value = reason;
}

function handleRecurrenceChange(value: string | null) {
  recurrenceError.value = '';
  editFormData.value.recurrenceRule = value;
}

function validateForm(): ShiftResourceFormData | null {
  formErrors.value = {};
  editFormData.value = normalizeShiftFormTimes(editFormData.value);

  const result = validateShiftFormData(editFormData.value, {
    timeZoneId: activeTimeZoneId.value,
    recurrenceError: recurrenceError.value,
    requireCancel: true,
  });

  if (!result.data) {
    formErrors.value = result.errors;
    return null;
  }

  return result.data;
}

async function handleSaveEdit() {
  const validated = validateForm();
  if (!validated) {
    return;
  }

  const payload = buildRequestPayload(validated);
  if (!payload) {
    return;
  }

  isSaving.value = true;
  apiError.value = '';

  try {
    if (payload.cancel) {
      const cancelled =
        payload.kind === 'series'
          ? await cancelShiftSeries(resolveShiftSeriesId(), payload.cancel)
          : await cancelShiftEntry(resolveShiftEntryId(), payload.cancel);
      if (!cancelled) {
        return;
      }

      calendarStore.refresh();
      emit('close');
      return;
    }

    const saved =
      payload.kind === 'series' ? await updateShiftSeries(payload.body) : await updateShiftEntry(payload.body);
    if (!saved) {
      return;
    }

    if (payload.kind === 'entry') {
      await syncEditedShiftEntryAssignmentLinks(validated);
    }

    const published =
      payload.kind === 'series'
        ? await publishShiftSeries(resolveShiftSeriesId(), payload.publish)
        : await publishShiftEntry(resolveShiftEntryId(), payload.publish);
    if (!published) {
      return;
    }

    calendarStore.refresh();
    emit('close');
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'An unexpected error occurred.';
  } finally {
    isSaving.value = false;
  }
}

async function handleDeleteShift() {
  if (!canDeleteShift.value) {
    return;
  }

  isSaving.value = true;
  apiError.value = '';

  try {
    let deleted: boolean;
    if (isActiveShift.value) {
      deleted =
        selectedOpenScope.value === 'series'
          ? await cancelShiftSeries(resolveShiftSeriesId(), true)
          : await cancelShiftEntry(resolveShiftEntryId(), true);
    } else {
      deleted = selectedOpenScope.value === 'series' ? await deleteShiftSeries() : await deleteShiftEntry();
    }

    if (!deleted) {
      return;
    }

    calendarStore.refresh();
    emit('close');
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'An unexpected error occurred.';
  } finally {
    isSaving.value = false;
  }
}

async function deleteShiftEntry() {
  const id = resolveShiftEntryId();
  if (!id) {
    apiError.value = 'Could not determine the shift entry to delete.';
    return false;
  }

  const result = await shiftApi.deleteShiftEntry(id);

  if (result.error.value) {
    apiError.value = result.error.value.message || 'Failed to delete shift entry.';
    return false;
  }

  return true;
}

async function deleteShiftSeries() {
  const id = resolveShiftSeriesId();
  if (!id) {
    apiError.value = 'Could not determine the shift to delete.';
    return false;
  }

  const result = await shiftApi.deleteShiftSeries(id);

  if (result.error.value) {
    apiError.value = result.error.value.message || 'Failed to delete shift series.';
    return false;
  }

  return true;
}

async function updateShiftSeries(body: ShiftSeriesRequest) {
  const id = resolveShiftSeriesId();
  if (!id) {
    apiError.value = 'Could not determine the shift series to update.';
    return null;
  }

  const result = await shiftApi.updateShiftSeries(id, body);

  if (result.error.value) {
    if (applyServerValidationErrors(result.data.value)) {
      return null;
    }

    apiError.value = result.error.value.message || 'Failed to update shift series.';
    return null;
  }

  return result.data.value ?? null;
}

async function updateShiftEntry(body: ShiftEntryRequest) {
  const id = resolveShiftEntryId();
  if (!id) {
    apiError.value = 'Could not determine the shift entry to update.';
    return null;
  }

  const result = await shiftApi.updateShiftEntry(id, body);

  if (result.error.value) {
    if (applyServerValidationErrors(result.data.value)) {
      return null;
    }

    apiError.value = result.error.value.message || 'Failed to update shift entry.';
    return null;
  }

  return result.data.value ?? null;
}

async function publishShiftSeries(id: number | null, shouldPublish: boolean) {
  if (!shouldPublish || !id) {
    return true;
  }

  const publishResult = await shiftApi.publishShiftSeries(id);

  if (publishResult.error.value) {
    apiError.value = publishResult.error.value.message || 'Shift updated but failed to publish.';
    return false;
  }

  return true;
}

async function publishShiftEntry(id: number | null, shouldPublish: boolean) {
  if (!shouldPublish || !id) {
    return true;
  }

  const publishResult = await shiftApi.publishShiftEntry(id);

  if (publishResult.error.value) {
    apiError.value = publishResult.error.value.message || 'Shift updated but failed to publish.';
    return false;
  }

  return true;
}

async function cancelShiftSeries(id: number | null, shouldCancel: boolean) {
  if (!shouldCancel || !id) {
    return true;
  }

  const cancelResult = await shiftApi.cancelShiftSeries(id);

  if (cancelResult.error.value) {
    apiError.value = cancelResult.error.value.message || 'Shift updated but failed to cancel.';
    return false;
  }

  return true;
}

async function cancelShiftEntry(id: number | null, shouldCancel: boolean) {
  if (!shouldCancel || !id) {
    return true;
  }

  const cancelResult = await shiftApi.cancelShiftEntry(id);

  if (cancelResult.error.value) {
    apiError.value = cancelResult.error.value.message || 'Shift updated but failed to cancel.';
    return false;
  }

  return true;
}

function buildRequestPayload(validated: ShiftResourceFormData) {
  const payload = buildUpdateShiftPayload({
    formData: validated,
    scope: selectedOpenScope.value === 'series' ? 'series' : 'entry',
    timeZoneId: activeTimeZoneId.value,
    locationId: activeLocationId.value,
    fallbackTitle: buildShiftTitle(props.event.title),
    shiftSeriesId: resolveShiftSeriesId(),
    existingRecurrenceRule: selectedSeries.value?.recurrenceRule ?? null,
  });

  if (!payload) {
    apiError.value = 'Could not resolve the selected date and time.';
  }

  return payload;
}

async function syncEditedShiftEntryAssignmentLinks(formData: ShiftResourceFormData) {
  const shiftEntryId = resolveShiftEntryId();
  if (!shiftEntryId) {
    throw new Error('Could not determine the shift entry for assignment links.');
  }

  const existingLinks = selectedEntry.value?.assignmentLinks ?? [];
  const existingLinksByAssignmentEntryId = new Map<number, typeof existingLinks>();

  for (const link of existingLinks) {
    if (typeof link.assignmentEntryId !== 'number') {
      continue;
    }

    const links = existingLinksByAssignmentEntryId.get(link.assignmentEntryId) ?? [];
    links.push(link);
    existingLinksByAssignmentEntryId.set(link.assignmentEntryId, links);
  }

  const desiredLinksByAssignmentEntryId = new Map(
    (formData.assignmentEntryLinks ?? []).flatMap((link) =>
      typeof link.assignmentEntryId === 'number' ? [[link.assignmentEntryId, link.assignedUserIds ?? []] as const] : [],
    ),
  );

  for (const [assignmentEntryId, assignedUserIds] of desiredLinksByAssignmentEntryId) {
    const existingLinksForAssignment = existingLinksByAssignmentEntryId.get(assignmentEntryId) ?? [];
    await syncAssignmentEntryLinks(
      assignmentEntryId,
      [
        {
          id: existingLinksForAssignment[0]?.id,
          shiftEntryId,
          assignedUserIds,
        },
      ],
      existingLinksForAssignment.flatMap((link) => (typeof link.id === 'number' ? [link.id] : [])),
    );
  }

  for (const [assignmentEntryId, existingLinksForAssignment] of existingLinksByAssignmentEntryId) {
    if (desiredLinksByAssignmentEntryId.has(assignmentEntryId)) {
      continue;
    }

    await syncAssignmentEntryLinks(
      assignmentEntryId,
      [],
      existingLinksForAssignment.flatMap((link) => (typeof link.id === 'number' ? [link.id] : [])),
    );
  }
}

function applyServerValidationErrors(rawError: unknown) {
  const mapped = mapToValidationErrors(rawError);
  if (!mapped) {
    return false;
  }

  formErrors.value = mapped;
  return true;
}

function resolveShiftEntryId() {
  if (!isCalendarSchedulingEvent(props.event)) {
    return null;
  }

  return parseNumericId(props.event.metadata.shiftEntryId);
}

function resolveShiftSeriesId() {
  if (!isCalendarSchedulingEvent(props.event)) {
    return null;
  }

  return parseNumericId(props.event.metadata.shiftSeriesId);
}

function parseNumericId(value: string | number | null | undefined) {
  if (value == null) {
    return null;
  }

  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function getInitialOpenScope(): ShiftOpenScope | null {
  if (props.initialOpenScope) {
    return props.initialOpenScope;
  }

  return resolveShiftSeriesId() ? null : 'event';
}
</script>

<template>
  <UaModal :title="modalTitle" width="760" @close="emit('close')">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
    </template>

    <div v-if="shouldShowOpenScopeChoice" class="shift-detail-modal__scope-choice">
      <p class="shift-detail-modal__scope-choice-text">This is one event in a series, What do you want to open?</p>
      <div class="shift-detail-modal__scope-choice-actions">
        <UaBtn variant="outlined" :disabled="isLoadingSeries" @click="selectOpenScope('event')">Only this event</UaBtn>
        <UaBtn color="primary" variant="flat" :loading="isLoadingSeries" @click="selectOpenScope('series')">
          The entire series
        </UaBtn>
      </div>
    </div>

    <div v-else class="shift-detail-modal">
      <div class="shift-detail-modal__tabs" role="tablist" aria-label="Shift detail tabs">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          :aria-selected="tab.id === activeTab"
          class="shift-detail-modal__tab"
          :class="{ 'shift-detail-modal__tab--active': tab.id === activeTab }"
          role="tab"
          type="button"
          @click="selectTab(tab.id)"
        >
          {{ tab.label }}
        </button>
      </div>

      <section v-if="activeTab === 'details'" class="shift-detail-modal__panel" aria-label="Shift details panel">
        <UaAlert v-if="isActiveShift" type="info">
          This shift has been published, and cannot be edited or deleted, only cancelled
        </UaAlert>
        <dl class="shift-detail-modal__details">
          <template v-for="detail in detailRows" :key="detail.label">
            <dt>{{ detail.label }}</dt>
            <dd>
              <RRuleEditor
                v-if="'recurrenceRule' in detail"
                :model-value="detail.recurrenceRule"
                :start-date="detail.recurrenceStartDate"
                read-only
              />
              <template v-else>{{ detail.value }}</template>
            </dd>
          </template>
        </dl>
      </section>

      <section v-else-if="activeTab === 'edit'" class="shift-detail-modal__panel" aria-label="Edit shift panel">
        <CalendarSchedulingShiftForm
          v-model="editFormData"
          id-prefix="edit-shift"
          :form-errors="formErrors"
          :disabled="isSaving"
          :show-recurrence="isSeriesScope"
          :location-options="locationOptions"
          :employee-options="employeeOptions"
          :is-loading-users="isLoadingUsers"
          :assignment-entry-options="assignmentEntryOptions"
          :assignment-series-options="assignmentSeriesOptions"
          :assignment-warning="assignmentWarning"
          :is-loading-assignments="isLoadingAssignments"
          :show-series-assignment="isSeriesScope"
          @recurrence-change="handleRecurrenceChange"
          @recurrence-invalid="handleRecurrenceInvalid"
        />
      </section>

      <section v-else-if="activeTab === 'delete'" class="shift-detail-modal__panel" aria-label="Delete shift panel">
        <dl class="shift-detail-modal__details">
          <template v-for="detail in detailRows" :key="detail.label">
            <dt>{{ detail.label }}</dt>
            <dd>
              <RRuleEditor
                v-if="'recurrenceRule' in detail"
                :model-value="detail.recurrenceRule"
                :start-date="detail.recurrenceStartDate"
                read-only
              />
              <template v-else>{{ detail.value }}</template>
            </dd>
          </template>
        </dl>

        <p v-if="deleteDisabledReason" class="shift-detail-modal__delete-warning">{{ deleteDisabledReason }}</p>
        <template v-else>
          <p class="shift-detail-modal__delete-warning">{{ deleteWarning }}</p>
          <v-checkbox v-model="isDeleteConfirmed" :label="deleteConfirmationLabel" hide-details />
        </template>
      </section>
    </div>

    <template v-if="!shouldShowOpenScopeChoice && (activeTab === 'edit' || activeTab === 'delete')" #actions>
      <template v-if="activeTab === 'edit'">
        <UaBtn variant="outlined" :disabled="isSaving" @click="selectTab('details')">Cancel</UaBtn>
        <UaBtn color="primary" variant="flat" :loading="isSaving" @click="handleSaveEdit">Save</UaBtn>
      </template>
      <template v-else>
        <UaBtn variant="outlined" :disabled="isSaving" @click="emit('close')">Close</UaBtn>
        <UaBtn color="error" variant="flat" :disabled="!canDeleteShift" :loading="isSaving" @click="handleDeleteShift">
          {{ isActiveShift ? 'Cancel Shift' : 'Delete' }}
        </UaBtn>
      </template>
    </template>
  </UaModal>
</template>

<style scoped>
.shift-detail-modal {
  display: grid;
  gap: var(--ua-spacing-lg);
}

.shift-detail-modal__scope-choice {
  display: grid;
  gap: var(--ua-spacing-lg);
}

.shift-detail-modal__scope-choice-text {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-semibold);
  margin: 0;
}

.shift-detail-modal__scope-choice-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-md);
  justify-content: flex-end;
}

.shift-detail-modal__tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-lg);
}

.shift-detail-modal__tab {
  background: transparent;
  border: 0;
  border-bottom: 2px solid transparent;
  color: var(--ua-text-primary);
  cursor: pointer;
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-semibold);
  padding: 0 0 var(--ua-spacing-xs);
}

.shift-detail-modal__tab--active {
  border-bottom-color: rgb(var(--v-theme-primary));
}

.shift-detail-modal__panel {
  display: grid;
  gap: var(--ua-spacing-md);
}

.shift-detail-modal__details {
  display: grid;
  gap: var(--ua-spacing-sm) var(--ua-spacing-lg);
  grid-template-columns: minmax(120px, max-content) minmax(0, 1fr);
  margin: 0;
}

.shift-detail-modal__details dt {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
}

.shift-detail-modal__details dd {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
  overflow-wrap: anywhere;
}

.shift-detail-modal__delete-warning {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
  margin: 0;
}

@media (max-width: 640px) {
  .shift-detail-modal__details {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
