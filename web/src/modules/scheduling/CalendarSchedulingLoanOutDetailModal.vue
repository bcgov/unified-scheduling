<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { LoanOutEntryResponse, LoanOutSeriesResponse } from '@/api-access/generated/models';
import { Permissions } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import {
  deleteLoanOutEntry,
  deleteLoanOutSeries,
  getLoanOutEntry,
  getLoanOutSeries,
  updateLoanOutEntry,
  updateLoanOutSeries,
} from '@/modules/loanout/api/loanOutApi';
import LoanOutForm, { type LoanOutFormData } from '@/modules/loanout/components/LoanOutForm.vue';
import { buildLoanOutRequest } from '@/modules/loanout/loanOutRequest';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { mapToValidationErrors } from '@/shared/validation/validationErrors';
import { useLocationsStore } from '@/stores/LocationsStore';
import { useUsersStore } from '@/stores/Users';
import CalendarSchedulingShiftDetailsPanel from './CalendarSchedulingShiftDetailsPanel.vue';
import { isCalendarSchedulingEvent } from './calendarSchedulingData';
import { parseFormDateTime } from './schedulingDateTime';
import { resolveSchedulingTimeZoneId } from './schedulingTimeZone';
import { formatUserOptionLabel } from './calendarSchedulingShiftForm';
import { RRule } from 'rrule';

const props = defineProps<{ event: CalendarEventBase }>();
const emit = defineEmits<{ (event: 'close'): void }>();
const accessControl = useAccessControl();
const calendarStore = useCalendarStore();
const locations = useLocationsStore();
const users = useUsersStore();
const metadata = computed(() => (isCalendarSchedulingEvent(props.event) ? props.event.metadata : undefined));
const entryId = computed(() => Number(metadata.value?.loanOutEntryId));
const seriesId = computed(() => Number(metadata.value?.loanOutSeriesId));
const scope = ref<'event' | 'series' | null>(seriesId.value > 0 ? null : 'event');
const tab = ref<'details' | 'edit' | 'delete'>('details');
const selectedEntry = ref<LoanOutEntryResponse | null>(null);
const selectedSeries = ref<LoanOutSeriesResponse | null>(null);
const entity = computed(() => (scope.value === 'series' ? selectedSeries.value : selectedEntry.value));
const isLoading = ref(false);
const isSaving = ref(false);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});
const hasValidationError = ref(false);
const deleteConfirmed = ref(false);
const loadedScope = ref<'event' | 'series' | null>(null);
const isDraft = computed(() => entity.value?.statusTypeCode?.toLowerCase() === 'draft');
const enabled = computed(() => accessControl.featureFlags.value?.LoanOut?.enabled ?? false);
const canEdit = computed(() => enabled.value && isDraft.value && accessControl.hasPermission(Permissions.LoanOutEdit));
const canDelete = computed(
  () => enabled.value && isDraft.value && accessControl.hasPermission(Permissions.LoanOutDelete),
);
const visibleTabs = computed<Array<{ id: 'details' | 'edit' | 'delete'; label: string }>>(() => [
  { id: 'details', label: 'Details' },
  ...(canEdit.value ? [{ id: 'edit' as const, label: 'Edit' }] : []),
  ...(canDelete.value ? [{ id: 'delete' as const, label: 'Delete' }] : []),
]);
const entityLabel = computed(() => (scope.value === 'series' ? 'Loan Out Series' : 'Loan Out'));
const modalTitle = computed(() =>
  scope.value == null
    ? 'Open Loan Out'
    : `${tab.value === 'edit' ? 'Edit' : tab.value === 'delete' ? 'Delete' : ''} ${entityLabel.value}${tab.value === 'details' ? ' Details' : ''}`.trim(),
);
const timeZone = computed(() => resolveSchedulingTimeZoneId(entity.value?.timeZoneId ?? props.event.timeZoneId));
const employeeOptions = computed(() =>
  users.allUsers.map((user) => ({ code: user.id, description: formatUserOptionLabel(user) })),
);
const formData = ref<LoanOutFormData>({});

function populateForm() {
  const value = entity.value;
  if (!value) return;
  const start = parseFormDateTime(value.startAtUtc ?? '', timeZone.value);
  formData.value = {
    userIds: value.userIds ?? [],
    loanOutLocationId: value.loanOutLocationId,
    date: start?.date ?? '',
    notes: value.notes ?? '',
    repeatMode: scope.value === 'series' ? 'custom' : 'never',
    recurrenceRule: scope.value === 'series' ? selectedSeries.value?.recurrenceRule : null,
  };
}

watch(
  scope,
  async (value, _previous, onCleanup) => {
    if (!value) return;
    const controller = new AbortController();
    onCleanup(() => controller.abort());
    apiError.value = '';
    isLoading.value = true;
    loadedScope.value = null;
    try {
      const id = value === 'series' ? seriesId.value : entryId.value;
      if (!Number.isInteger(id) || id <= 0) throw new Error('Could not determine the loan-out record to open.');
      await users.ensureAllUsers(controller.signal);
      if (value === 'series') {
        const result = await getLoanOutSeries(id, controller.signal);
        if (result.error.value) throw result.error.value;
        if (!result.data.value) throw new Error('Loan-out series was not found.');
        selectedSeries.value = result.data.value;
      } else {
        const result = await getLoanOutEntry(id, controller.signal);
        if (result.error.value) throw result.error.value;
        if (!result.data.value) throw new Error('Loan-out entry was not found.');
        selectedEntry.value = result.data.value;
      }
      if (controller.signal.aborted) return;
      loadedScope.value = value;
      populateForm();
    } catch (error: unknown) {
      if (!controller.signal.aborted)
        apiError.value = error instanceof Error ? error.message : 'Failed to load loan-out.';
    } finally {
      if (!controller.signal.aborted) isLoading.value = false;
    }
  },
  { immediate: true },
);

const detailRows = computed(() => {
  const value = entity.value;
  const locationId = value?.loanOutLocationId ?? metadata.value?.loanOutLocationId;
  let recurrence = 'Does not repeat';
  if (scope.value === 'series' && selectedSeries.value?.recurrenceRule) {
    try {
      recurrence = RRule.fromString(selectedSeries.value.recurrenceRule).toText();
    } catch {
      recurrence = selectedSeries.value.recurrenceRule;
    }
  }
  return [
    {
      label: 'Location',
      value: locations.entitiesMap[value?.locationId ?? props.event.locationId ?? '']?.name ?? 'Unknown location',
    },
    {
      label: 'Loan-out Location',
      value: locations.entitiesMap[locationId ?? '']?.name ?? `Location ${locationId ?? '-'}`,
    },
    {
      label: 'Employee(s)',
      value:
        (value?.userIds ?? [])
          .map((id) => {
            const user = users.getUserById(id);
            return user ? `${user.firstName} ${user.lastName}` : id;
          })
          .join(', ') || 'None',
    },
    { label: 'Date', value: parseFormDateTime(value?.startAtUtc ?? props.event.start, timeZone.value)?.date ?? '-' },
    { label: 'Repeat', value: recurrence },
    { label: 'Notes', value: value?.notes?.trim() || 'None' },
  ];
});

function selectTab(value: 'details' | 'edit' | 'delete') {
  if (isLoading.value || isSaving.value) return;
  if ((value === 'edit' && !canEdit.value) || (value === 'delete' && !canDelete.value)) return;
  tab.value = value;
  apiError.value = '';
  formErrors.value = {};
  deleteConfirmed.value = false;
  if (value === 'edit') populateForm();
}

async function save() {
  if (!canEdit.value || loadedScope.value !== scope.value || hasValidationError.value) return;
  apiError.value = '';
  formErrors.value = {};
  const built = buildLoanOutRequest(formData.value, {
    locationId: entity.value?.locationId ?? props.event.locationId,
    timeZoneId: timeZone.value,
    loanOutSeriesId: selectedEntry.value?.loanOutSeriesId,
    employeeOptions: employeeOptions.value,
  });
  if (built.error !== undefined) {
    apiError.value = built.error;
    formErrors.value = built.fieldErrors ?? {};
    return;
  }
  if ((scope.value === 'series') !== (built.kind === 'series')) {
    apiError.value = 'Keep the existing recurrence scope when editing loan-out.';
    return;
  }
  isSaving.value = true;
  try {
    const result =
      built.kind === 'series'
        ? await updateLoanOutSeries(seriesId.value, built.request)
        : await updateLoanOutEntry(entryId.value, built.request);
    if (result.error.value) {
      const validationErrors = mapToValidationErrors(result.error.value.data);
      formErrors.value = validationErrors ?? {};
      apiError.value =
        Object.values(validationErrors ?? {})[0] || result.error.value.message || 'Failed to save loan-out.';
      return;
    }
    calendarStore.refresh();
    emit('close');
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'Failed to save loan-out.';
  } finally {
    isSaving.value = false;
  }
}

async function remove() {
  if (!canDelete.value || !deleteConfirmed.value || loadedScope.value !== scope.value) return;
  isSaving.value = true;
  apiError.value = '';
  try {
    const result =
      scope.value === 'series' ? await deleteLoanOutSeries(seriesId.value) : await deleteLoanOutEntry(entryId.value);
    if (result.error.value) {
      const validationErrors = mapToValidationErrors(result.error.value.data);
      apiError.value =
        Object.values(validationErrors ?? {})[0] || result.error.value.message || 'Failed to delete loan-out.';
      return;
    }
    calendarStore.refresh();
    emit('close');
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'Failed to delete loan-out.';
  } finally {
    isSaving.value = false;
  }
}
</script>

<template>
  <UaModal :title="modalTitle" width="840" :loading="isSaving" @close="emit('close')">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">{{ apiError }}</UaAlert>
    </template>
    <div v-if="scope == null" class="loanout-modal__scope-choice">
      <p class="loanout-modal__scope-choice-text">This is one occurrence in a series. What do you want to open?</p>
      <div class="loanout-modal__scope-choice-actions">
        <UaBtn variant="outlined" @click="scope = 'event'">Only this occurrence</UaBtn>
        <UaBtn color="primary" variant="flat" @click="scope = 'series'">The entire series</UaBtn>
      </div>
    </div>
    <div v-else class="loanout-modal">
      <div class="loanout-modal__tabs" role="tablist" aria-label="Loan-out detail tabs">
        <button
          v-for="item in visibleTabs"
          :key="item.id"
          type="button"
          role="tab"
          class="loanout-modal__tab"
          :class="{ 'loanout-modal__tab--active': tab === item.id }"
          :aria-selected="tab === item.id"
          :disabled="isSaving || isLoading"
          @click="selectTab(item.id)"
        >
          {{ item.label }}
        </button>
      </div>
      <CalendarSchedulingShiftDetailsPanel v-if="tab === 'details'" :detail-rows="detailRows" :is-loading="isLoading" />
      <LoanOutForm
        v-else-if="tab === 'edit'"
        v-model="formData"
        :disabled="isSaving || isLoading"
        :employee-options="employeeOptions"
        :form-errors="formErrors"
        :time-zone-id="timeZone"
        :show-repeat="scope === 'series'"
        show-employees
        @validation-change="hasValidationError = $event"
      />
      <section v-else class="loanout-modal__delete-panel" aria-label="Delete loan-out panel">
        <CalendarSchedulingShiftDetailsPanel :detail-rows="detailRows" :is-loading="isLoading" />
        <p class="loanout-modal__delete-warning">
          {{
            scope === 'series'
              ? "This will delete all draft occurrences in the series. This can't be undone."
              : "This can't be undone."
          }}
        </p>
        <v-checkbox
          v-model="deleteConfirmed"
          :label="
            scope === 'series'
              ? 'I understand this loan-out series will be permanently deleted for all assigned users.'
              : 'I understand this loan-out will be permanently deleted for all assigned users.'
          "
          hide-details
        />
      </section>
    </div>
    <template v-if="scope != null && tab !== 'details'" #actions>
      <UaBtn variant="outlined" :disabled="isSaving" @click="tab === 'delete' ? emit('close') : selectTab('details')">
        {{ tab === 'delete' ? 'Close' : 'Cancel' }}
      </UaBtn>
      <UaBtn
        v-if="tab === 'edit'"
        color="primary"
        variant="flat"
        :loading="isSaving"
        :disabled="hasValidationError || isLoading"
        @click="save"
        >Save</UaBtn
      >
      <UaBtn
        v-else
        color="error"
        variant="flat"
        :loading="isSaving"
        :disabled="!deleteConfirmed || isLoading"
        @click="remove"
        >Delete</UaBtn
      >
    </template>
  </UaModal>
</template>

<style scoped>
.loanout-modal__tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-lg);
}

.loanout-modal__tab {
  background: transparent;
  border: 0;
  border-bottom: 2px solid transparent;
  color: var(--ua-text-primary);
  cursor: pointer;
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-semibold);
  padding: 0 0 var(--ua-spacing-xs);
}

.loanout-modal__tab--active {
  border-bottom-color: rgb(var(--v-theme-primary));
}

.loanout-modal__delete-panel {
  display: grid;
  gap: var(--ua-spacing-md);
}

.loanout-modal__delete-warning {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
  margin: 0;
}

.loanout-modal__scope-choice {
  display: grid;
  gap: var(--ua-spacing-md);
}

.loanout-modal__scope-choice-text {
  margin: 0;
}

.loanout-modal__scope-choice-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-sm);
}
</style>
