<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import {
  Permissions,
  type LoanOutEntryResponse,
  type LoanOutSeriesResponse,
  type UserResponse,
} from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { resolveSchedulingTimeZoneId } from '@/modules/scheduling/schedulingTimeZone';
import { parseFormDateTime } from '@/modules/scheduling/schedulingDateTime';
import { formatUserOptionLabel } from '@/modules/scheduling/calendarSchedulingShiftForm';
import {
  createLoanOutEntry,
  createLoanOutSeries,
  getLoanOutEntry,
  getLoanOutSeries,
  updateLoanOutEntry,
  updateLoanOutSeries,
} from '@/modules/loanout/api/loanOutApi';
import { buildLoanOutRequest, resolveLoanOutSaveError } from '@/modules/loanout/loanOutRequest';
import LoanOutForm, { type LoanOutFormData } from '@/modules/loanout/components/LoanOutForm.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import { useUsersStore } from '@/stores/Users';

const props = defineProps<{
  user: UserResponse;
  entry?: (LoanOutEntryResponse & { series?: LoanOutSeriesResponse }) | null;
  scope?: 'event' | 'series';
}>();
const emit = defineEmits<{
  (event: 'close'): void;
  (event: 'save'): void;
}>();
const locationsStore = useLocationsStore();
const usersStore = useUsersStore();
const accessControl = useAccessControl();
const isSaving = ref(false);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});
const isEdit = computed(() => props.entry != null);
const isSeriesEdit = computed(() => isEdit.value && props.scope === 'series');
const loadedSeries = ref<LoanOutSeriesResponse | null>(null);
const loadedEntry = ref<LoanOutEntryResponse | null>(null);
const isLoadingEntry = ref(false);
const isLoadingSeries = ref(false);
const hasFormValidationError = ref(false);
const canSave = computed(
  () =>
    Boolean(accessControl.featureFlags.value?.LoanOut?.enabled) &&
    accessControl.hasPermission(isEdit.value ? Permissions.UsersLoanOutEdit : Permissions.UsersLoanOutCreate),
);
const locationId = computed(() => {
  const existing = loadedSeries.value?.locationId ?? loadedEntry.value?.locationId ?? props.entry?.locationId;
  if (existing != null) return existing;
  const selected = Number(locationsStore.selectedLocationId);
  if (locationsStore.selectedLocationId !== null && locationsStore.selectedLocationId !== '' && selected > 0)
    return selected;
  return props.user.homeLocationId ?? null;
});
const timeZoneId = computed(() =>
  resolveSchedulingTimeZoneId(
    loadedSeries.value?.timeZoneId ?? loadedEntry.value?.timeZoneId ?? props.entry?.timeZoneId,
    locationId.value ? locationsStore.entitiesMap[locationId.value]?.timezone : undefined,
  ),
);
function buildInitialFormData(series?: LoanOutSeriesResponse): LoanOutFormData {
  const entry = loadedEntry.value ?? props.entry;
  const source = series ?? (isSeriesEdit.value ? props.entry?.series : null) ?? entry;
  const zone = resolveSchedulingTimeZoneId(source?.timeZoneId, timeZoneId.value);
  const start = source?.startAtUtc ? parseFormDateTime(source.startAtUtc, zone) : null;
  const recurrenceRule = isSeriesEdit.value
    ? (series?.recurrenceRule ?? props.entry?.series?.recurrenceRule ?? null)
    : null;
  return {
    userIds: source?.userIds?.length ? [...source.userIds] : [props.user.id],
    loanOutLocationId: source?.loanOutLocationId ?? null,
    date: start?.date ?? '',
    repeatMode: recurrenceRule ? 'custom' : 'never',
    recurrenceRule,
    notes: source?.notes ?? '',
  };
}
const formData = ref<LoanOutFormData>(buildInitialFormData());
onMounted(async () => {
  if (isSeriesEdit.value) {
    if (props.entry?.loanOutSeriesId == null) {
      apiError.value = 'Cannot load this loan-out series because its ID is missing.';
      return;
    }
    isLoadingSeries.value = true;
    try {
      const result = await getLoanOutSeries(props.entry.loanOutSeriesId);
      if (result.error.value) {
        apiError.value = result.error.value.message || 'Failed to load recurring loan-out.';
        return;
      }
      if (result.data.value) {
        loadedSeries.value = result.data.value;
        formData.value = buildInitialFormData(result.data.value);
      } else apiError.value = 'Loan-out series was not found.';
    } catch (error: unknown) {
      apiError.value = error instanceof Error ? error.message : 'Failed to load recurring loan-out.';
    } finally {
      isLoadingSeries.value = false;
    }
    return;
  }
  if (!isEdit.value || props.entry?.id == null) return;
  isLoadingEntry.value = true;
  try {
    const result = await getLoanOutEntry(props.entry.id);
    if (result.error.value) {
      apiError.value = result.error.value.message || 'Failed to load loan-out.';
      return;
    }
    if (result.data.value) {
      loadedEntry.value = result.data.value;
      formData.value = buildInitialFormData();
    } else apiError.value = 'Loan-out was not found.';
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'Failed to load loan-out.';
  } finally {
    isLoadingEntry.value = false;
  }
});

async function handleSave() {
  if (!canSave.value || isSaving.value || hasFormValidationError.value || isLoadingEntry.value || isLoadingSeries.value)
    return;
  if (isEdit.value && !(isSeriesEdit.value ? loadedSeries.value : loadedEntry.value)) return;
  apiError.value = '';
  formErrors.value = {};
  const built = buildLoanOutRequest(formData.value, {
    locationId: locationId.value,
    timeZoneId: timeZoneId.value,
    loanOutSeriesId: isSeriesEdit.value
      ? null
      : (loadedEntry.value?.loanOutSeriesId ?? props.entry?.loanOutSeriesId ?? null),
    employeeOptions: (formData.value.userIds ?? []).map((id) => {
      const user = id === props.user.id ? props.user : usersStore.getUserById(id);
      return { code: id, description: user ? formatUserOptionLabel(user) : id };
    }),
  });
  if (built.error !== undefined) {
    apiError.value = built.error;
    formErrors.value = built.fieldErrors ?? {};
    return;
  }
  isSaving.value = true;
  try {
    if (isEdit.value && (isSeriesEdit.value ? built.kind !== 'series' : built.kind !== 'entry')) {
      apiError.value = isSeriesEdit.value
        ? 'Choose a recurrence rule to update the entire series.'
        : 'Converting an individual loan-out into a repeating series is not supported yet.';
      return;
    }
    const result =
      built.kind === 'series'
        ? isSeriesEdit.value && props.entry?.loanOutSeriesId != null
          ? await updateLoanOutSeries(props.entry.loanOutSeriesId, built.request)
          : await createLoanOutSeries(built.request)
        : isEdit.value && props.entry?.id != null
          ? await updateLoanOutEntry(props.entry.id, built.request)
          : await createLoanOutEntry(built.request);
    if (result.error.value) {
      const resolved = resolveLoanOutSaveError(result.data.value, result.error.value);
      apiError.value = resolved.message;
      formErrors.value = resolved.fieldErrors;
      return;
    }
    emit('save');
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'An unexpected error occurred.';
  } finally {
    isSaving.value = false;
  }
}
</script>

<template>
  <UaModal
    :title="isEdit ? (isSeriesEdit ? 'Edit Loan-out Series' : 'Edit Loan-out') : 'Add Loan-out'"
    :loading="isSaving"
    @close="emit('close')"
  >
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">{{ apiError }}</UaAlert>
    </template>
    <LoanOutForm
      v-model="formData"
      :home-location-id="user.homeLocationId"
      :disabled="isSaving || isLoadingEntry || isLoadingSeries || !canSave"
      :show-repeat="!isEdit || isSeriesEdit"
      :time-zone-id="timeZoneId"
      :form-errors="formErrors"
      @validation-change="hasFormValidationError = $event"
    />
    <template #actions>
      <UaBtn variant="outlined" :disabled="isSaving || isLoadingEntry || isLoadingSeries" @click="emit('close')"
        >Cancel</UaBtn
      >
      <UaBtn
        :loading="isSaving"
        :disabled="
          !canSave ||
          hasFormValidationError ||
          isLoadingEntry ||
          isLoadingSeries ||
          (isEdit && !isSeriesEdit && !loadedEntry) ||
          (isSeriesEdit && !loadedSeries)
        "
        @click="handleSave"
        >Save</UaBtn
      >
    </template>
  </UaModal>
</template>
