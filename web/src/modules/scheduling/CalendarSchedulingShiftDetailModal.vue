<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { ShiftEntryRequest } from '@/api-access/generated/models/shiftEntryRequest';
import type { ShiftSeriesRequest } from '@/api-access/generated/models/shiftSeriesRequest';
import type { ShiftSeriesResponse } from '@/api-access/generated/models/shiftSeriesResponse';
import type { UserResponse } from '@/api-access/generated/models/userResponse';
import { getApiUsers } from '@/api-access/generated/users/users';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { formatCalendarEventTimeRange, toDateTime } from '@/utils/date';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import RRuleEditor from '@/components/recurrence/RRuleEditor.vue';
import { isCalendarSchedulingEvent } from './calendarSchedulingData';
import CalendarSchedulingShiftForm from './CalendarSchedulingShiftForm.vue';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import { useLocationsStore } from '@/stores/LocationsStore';
import { mapToValidationErrors } from '@/shared/validation/validationErrors';
import type { SelectOption } from '@/types/select';
import {
  buildUpdateShiftPayload,
  buildShiftTitle,
  createShiftFormDataFromEvent,
  createShiftFormDataFromSeries,
  formatUserOptionLabel,
  normalizeShiftFormTimes,
  validateShiftFormData,
  type ShiftResourceFormData,
} from './calendarSchedulingShiftForm';
import * as shiftApi from './calendarSchedulingShiftApi';

type ShiftDetailTabId = 'details' | 'edit' | 'transfer' | 'copy' | 'delete';
type ShiftOpenScope = 'event' | 'series';

type ShiftDetailRow = {
  label: string;
  value: string;
  recurrenceRule?: string | null;
  recurrenceStartDate?: string | null;
};

const props = defineProps<{
  event: CalendarEventBase;
}>();

const emit = defineEmits<{
  (event: 'close'): void;
}>();

const calendarStore = useCalendarStore();
const locationsStore = useLocationsStore();

const tabs: Array<{ id: ShiftDetailTabId; label: string }> = [
  { id: 'details', label: 'Details' },
  { id: 'edit', label: 'Edit' },
  { id: 'transfer', label: 'Transfer' },
  { id: 'copy', label: 'Copy' },
  { id: 'delete', label: 'Delete' },
];

const activeTab = ref<ShiftDetailTabId>('details');
const selectedOpenScope = ref<ShiftOpenScope | null>(getInitialOpenScope());
const placeholderNotice = ref('');
const isSaving = ref(false);
const apiError = ref('');
const isLoadingUsers = ref(false);
const isLoadingSeries = ref(false);
const formErrors = ref<Record<string, string>>({});
const recurrenceError = ref('');
const isDeleteConfirmed = ref(false);
const availableUsers = ref<UserResponse[]>([]);
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
const eventBelongsToSeries = computed(() => resolveShiftSeriesId() !== null);
const shouldShowOpenScopeChoice = computed(() => eventBelongsToSeries.value && selectedOpenScope.value === null);
const isSeriesScope = computed(() => selectedOpenScope.value === 'series');
const modalTitle = computed(() => (isSeriesScope.value ? 'Shift Series Details' : 'Shift Details'));
const deleteDisabledReason = computed(() => {
  const statusTypeCode =
    selectedOpenScope.value === 'series' ? selectedSeries.value?.statusTypeCode : props.event.statusTypeCode;
  const normalizedStatus = String(statusTypeCode ?? '').toLowerCase();

  if (normalizedStatus && normalizedStatus !== 'draft') {
    return selectedOpenScope.value === 'series'
      ? 'Only draft shift series can be deleted.'
      : 'Only draft shift entries can be deleted.';
  }

  return '';
});
const canDeleteShift = computed(() => !deleteDisabledReason.value && isDeleteConfirmed.value);

const employeeOptions = computed<SelectOption[]>(() => {
  const options = availableUsers.value.map((user) => ({
    code: user.id,
    description: formatUserOptionLabel(user),
  }));

  for (const userId of editFormData.value.userIds ?? []) {
    if (!options.some((option) => option.code === userId)) {
      options.unshift({ code: userId, description: userId });
    }
  }

  return options;
});

watch(
  () => props.event,
  (event) => {
    selectedSeries.value = null;
    editFormData.value = createShiftFormDataFromEvent(event, timeZoneId.value);
    activeTab.value = 'details';
    selectedOpenScope.value = getInitialOpenScope();
    apiError.value = '';
    formErrors.value = {};
    recurrenceError.value = '';
    isDeleteConfirmed.value = false;
  },
);

watch(
  activeLocationId,
  async (locationId) => {
    await loadEmployeeOptions(locationId);
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

const detailRows = computed<ShiftDetailRow[]>(() => {
  if (selectedOpenScope.value === 'series' && selectedSeries.value) {
    return [
      { label: 'Assignee(s)', value: formatAssigneeIds(selectedSeries.value.userIds ?? []) },
      {
        label: 'Date',
        value: formatEventDate(selectedSeries.value.startAtUtc ?? props.event.start, activeTimeZoneId.value),
      },
      {
        label: 'Time',
        value: formatCalendarEventTimeRange(
          selectedSeries.value.startAtUtc ?? props.event.start,
          selectedSeries.value.endAtUtc ?? props.event.end,
          {
            allDay: selectedSeries.value.allDay ?? false,
            timeZone: activeTimeZoneId.value,
          },
        ),
      },
      { label: 'Notes', value: selectedSeries.value.notes?.trim() || 'None' },
      {
        label: 'Repeat',
        value: '',
        recurrenceRule: selectedSeries.value.recurrenceRule ?? null,
        recurrenceStartDate: selectedSeries.value.startAtUtc ?? props.event.start,
      },
    ];
  }

  return [
    { label: 'Assignee(s)', value: formatAssignees(props.event) },
    { label: 'Date', value: formatEventDate(props.event.start, props.event.timeZoneId) },
    {
      label: 'Time',
      value: formatCalendarEventTimeRange(props.event.start, props.event.end, {
        allDay: props.event.allDay,
        timeZone: props.event.timeZoneId,
      }),
    },
    { label: 'Notes', value: props.event.notes?.trim() || 'None' },
  ];
});

const placeholderHeading = computed(() => {
  switch (activeTab.value) {
    case 'edit':
      return 'Edit shift';
    case 'transfer':
      return 'Transfer shift';
    case 'copy':
      return 'Copy shift';
    case 'delete':
      return 'Delete shift';
    default:
      return 'Shift details';
  }
});

const placeholderDescription = computed(() => {
  switch (activeTab.value) {
    case 'edit':
      return 'Editing is not implemented yet.';
    case 'transfer':
      return 'Transfer is not implemented yet.';
    case 'copy':
      return 'Copy is not implemented yet.';
    case 'delete':
      return 'Delete is not implemented yet. This tab is a placeholder for the future delete flow.';
    default:
      return '';
  }
});

function selectTab(tabId: ShiftDetailTabId) {
  activeTab.value = tabId;
  placeholderNotice.value = '';
  apiError.value = '';
  isDeleteConfirmed.value = false;
}

async function selectOpenScope(scope: ShiftOpenScope) {
  apiError.value = '';
  placeholderNotice.value = '';

  if (scope === 'series') {
    const series = await loadSelectedSeries();
    if (!series) {
      return;
    }
  } else {
    selectedSeries.value = null;
  }

  selectedOpenScope.value = scope;
  editFormData.value = createEditFormData();
  activeTab.value = 'details';
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
    const deleted = selectedOpenScope.value === 'series' ? await deleteShiftSeries() : await deleteShiftEntry();

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

function applyServerValidationErrors(rawError: unknown) {
  const mapped = mapToValidationErrors(rawError);
  if (!mapped) {
    return false;
  }

  formErrors.value = mapped;
  return true;
}

async function loadEmployeeOptions(locationId: number | null) {
  isLoadingUsers.value = true;

  try {
    const { data, error, execute } = getApiUsers(
      {
        IsEnabled: true,
        LocationId: locationId ?? undefined,
      },
      {
        options: { immediate: false },
      },
    );

    await execute();

    if (error.value) {
      throw error.value;
    }

    availableUsers.value = data.value ?? [];
  } catch (error: unknown) {
    availableUsers.value = [];
    apiError.value = error instanceof Error ? error.message : 'Failed to load employees.';
  } finally {
    isLoadingUsers.value = false;
  }
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
  return resolveShiftSeriesId() ? null : 'event';
}

function formatEventDate(value: string, timeZone?: string) {
  const dateTime = toDateTime(value, timeZone);

  if (!dateTime.isValid) {
    return 'Unknown';
  }

  return dateTime.toFormat('LLLL d, yyyy');
}

function formatAssignees(event: CalendarEventBase) {
  return formatAssigneeIds(getEventUserIds(event));
}

function formatAssigneeIds(userIds: string[]) {
  if (userIds.length === 0) {
    return 'None';
  }

  return userIds.map(formatAssignee).join(', ');
}

function getEventUserIds(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return event.resourceIds ?? [];
  }

  if (event.metadata.userIds?.length) {
    return event.metadata.userIds;
  }

  return event.metadata.userId ? [event.metadata.userId] : (event.resourceIds ?? []);
}

function formatAssignee(userId: string) {
  const option = employeeOptions.value.find((candidate) => String(candidate.code) === userId);
  return option?.description || userId;
}
</script>

<template>
  <UaModal :title="modalTitle" width="760" @close="emit('close')">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
      <UaAlert v-if="placeholderNotice" type="info" @close="placeholderNotice = ''">
        {{ placeholderNotice }}
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
          :employee-options="employeeOptions"
          :is-loading-users="isLoadingUsers"
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
          <p class="shift-detail-modal__delete-warning">This can't be undone.</p>
          <v-checkbox
            v-model="isDeleteConfirmed"
            label="I understand this shift will be permanently deleted."
            hide-details
          />
        </template>
      </section>

      <section v-else class="shift-detail-modal__panel" :aria-label="`${placeholderHeading} panel`">
        <div class="shift-detail-modal__placeholder">
          <h3 class="shift-detail-modal__placeholder-heading">{{ placeholderHeading }}</h3>
          <p class="shift-detail-modal__placeholder-text">{{ placeholderDescription }}</p>
        </div>
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
          Delete
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

.shift-detail-modal__placeholder {
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  display: grid;
  gap: var(--ua-spacing-sm);
  padding: var(--ua-spacing-lg);
}

.shift-detail-modal__placeholder-heading {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-bold);
  margin: 0;
}

.shift-detail-modal__placeholder-text {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
}

@media (max-width: 640px) {
  .shift-detail-modal__details {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
