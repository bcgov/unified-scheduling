<script setup lang="ts">
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { computed, onMounted, ref } from 'vue';
import type {
  SchedulingLeaveEntryResponse as LeaveEntryResponse,
  SchedulingLeaveSeriesResponse as LeaveSeriesResponse,
  UserResponse,
} from '@/api-access/generated/models';
import { resolveSchedulingTimeZoneId } from '@/modules/scheduling/schedulingTimeZone';
import { defaultEndTime, defaultStartTime, parseFormDateTime } from '@/modules/scheduling/schedulingDateTime';
import { formatUserOptionLabel } from '@/modules/scheduling/calendarSchedulingShiftForm';
import {
  createLeaveEntry,
  createLeaveSeries,
  getLeaveEntry,
  getLeaveSeries,
  updateLeaveEntry,
  updateLeaveSeries,
} from '@/modules/leave/api/leaveApi';
import { buildLeaveRequest } from '@/modules/leave/leaveRequest';
import LeaveFormComponent, { type LeaveFormData } from '@/modules/leave/components/LeaveForm.vue';
import { useLocationsStore } from '@/stores/LocationsStore';

const props = defineProps<{
  user: UserResponse;
  entry?: LeaveEntryResponse | null;
  scope?: 'event' | 'series';
}>();

const emit = defineEmits<{
  (event: 'close'): void;
  (event: 'save'): void;
}>();

const locationsStore = useLocationsStore();
const isSaving = ref(false);
const apiError = ref('');
const isSeriesEdit = computed(() => props.entry != null && props.scope === 'series');
const loadedSeries = ref<LeaveSeriesResponse | null>(null);
const loadedEntry = ref<LeaveEntryResponse | null>(null);
const isEdit = computed(() => props.entry != null);
const isLoadingEntry = ref(false);
const isLoadingSeries = ref(false);
const hasFormValidationError = ref(false);

const buildInitialFormData = (series?: LeaveSeriesResponse): LeaveFormData => {
  const entry = loadedEntry.value ?? props.entry;
  const entrySeries =
    entry && 'series' in entry ? (entry as LeaveEntryResponse & { series?: LeaveSeriesResponse }).series : undefined;
  if (!entry) {
    return {
      userIds: [props.user.id],
      leaveType: null,
      date: '',
      allDay: false,
      startTime: defaultStartTime,
      endTime: defaultEndTime,
      repeatMode: 'never',
      recurrenceRule: null,
      notes: '',
    };
  }

  const zone = resolveSchedulingTimeZoneId(
    series?.timeZoneId ?? (isSeriesEdit.value ? entrySeries?.timeZoneId : null) ?? entry.timeZoneId,
  );
  const startAtUtc = series?.startAtUtc ?? (isSeriesEdit.value ? entrySeries?.startAtUtc : null) ?? entry.startAtUtc;
  const endAtUtc = series?.endAtUtc ?? (isSeriesEdit.value ? entrySeries?.endAtUtc : null) ?? entry.endAtUtc;
  const start = startAtUtc ? parseFormDateTime(startAtUtc, zone) : null;
  const end = endAtUtc ? parseFormDateTime(endAtUtc, zone) : null;
  const allDay = series?.allDay ?? (isSeriesEdit.value ? entrySeries?.allDay : null) ?? entry.allDay ?? false;
  return {
    userIds: [series?.userIds, isSeriesEdit.value ? entrySeries?.userIds : null, entry.userIds].find(
      (ids) => ids && ids.length > 0,
    ) ?? [props.user.id],
    leaveType: series?.leaveTypeId ?? entry.leaveTypeId ?? null,
    date:
      series?.leaveDate ?? (isSeriesEdit.value ? entrySeries?.leaveDate : null) ?? entry.leaveDate ?? start?.date ?? '',
    allDay,
    startTime: allDay ? defaultStartTime : (start?.time ?? defaultStartTime),
    endTime: allDay ? defaultEndTime : (end?.time ?? defaultEndTime),
    repeatMode: isSeriesEdit.value
      ? (series?.recurrenceRule ?? entrySeries?.recurrenceRule)
        ? 'custom'
        : 'never'
      : 'never',
    recurrenceRule: isSeriesEdit.value ? (series?.recurrenceRule ?? entrySeries?.recurrenceRule ?? null) : null,
    notes: series?.notes ?? entry.notes ?? '',
  };
};

const formData = ref<LeaveFormData>(buildInitialFormData());
onMounted(async () => {
  if (isSeriesEdit.value && props.entry?.leaveSeriesId != null) {
    isLoadingSeries.value = true;
    try {
      const result = await getLeaveSeries(props.entry.leaveSeriesId);
      if (result.error.value) {
        apiError.value = result.error.value.message || 'Failed to load recurring leave.';
        return;
      }
      if (result.data.value) {
        loadedSeries.value = result.data.value;
        formData.value = buildInitialFormData(result.data.value);
      } else {
        apiError.value = 'Leave series was not found.';
      }
    } catch (error) {
      apiError.value = error instanceof Error ? error.message : 'Failed to load recurring leave.';
    } finally {
      isLoadingSeries.value = false;
    }
    return;
  }

  if (!isEdit.value || props.entry?.id == null) return;

  isLoadingEntry.value = true;
  try {
    const result = await getLeaveEntry(props.entry.id);
    if (result.error.value) {
      apiError.value = result.error.value.message || 'Failed to load leave.';
      return;
    }
    if (result.data.value) {
      loadedEntry.value = result.data.value;
      formData.value = buildInitialFormData();
    } else {
      apiError.value = 'Leave was not found.';
    }
  } catch (error) {
    apiError.value = error instanceof Error ? error.message : 'Failed to load leave.';
  } finally {
    isLoadingEntry.value = false;
  }
});

const locationId = computed<number | null>(() => {
  const entry = loadedEntry.value ?? props.entry;
  if (entry?.locationId != null) {
    return entry.locationId;
  }
  const selected = Number(locationsStore.selectedLocationId);
  if (locationsStore.selectedLocationId !== '' && Number.isFinite(selected)) {
    return selected;
  }
  return props.user.homeLocationId ?? null;
});

const timeZoneId = computed(() =>
  resolveSchedulingTimeZoneId(
    loadedSeries.value?.timeZoneId ?? loadedEntry.value?.timeZoneId ?? props.entry?.timeZoneId,
    locationId.value ? locationsStore.entitiesMap[locationId.value]?.timezone : undefined,
  ),
);

const handleSave = async () => {
  if (isLoadingEntry.value || isLoadingSeries.value || (isSeriesEdit.value && !loadedSeries.value)) return;
  apiError.value = '';
  const built = buildLeaveRequest(formData.value, {
    locationId: locationId.value,
    timeZoneId: timeZoneId.value,
    leaveSeriesId: isSeriesEdit.value ? null : (loadedEntry.value?.leaveSeriesId ?? props.entry?.leaveSeriesId ?? null),
    employeeOptions: [{ code: props.user.id, description: formatUserOptionLabel(props.user) }],
  });
  if (built.error !== undefined) {
    apiError.value = built.error;
    return;
  }

  isSaving.value = true;
  try {
    if (isEdit.value && (isSeriesEdit.value ? built.kind !== 'series' : built.kind !== 'entry')) {
      apiError.value = isSeriesEdit.value
        ? 'Choose a recurrence rule to update the entire series.'
        : 'Converting an individual leave into a repeating series is not supported yet.';
      return;
    }
    const result =
      built.kind === 'series'
        ? isSeriesEdit.value && props.entry?.leaveSeriesId != null
          ? await updateLeaveSeries(props.entry.leaveSeriesId, built.request)
          : await createLeaveSeries(built.request)
        : isEdit.value && props.entry?.id != null
          ? await updateLeaveEntry(props.entry.id, built.request)
          : await createLeaveEntry(built.request);
    if (result.error.value) {
      apiError.value = result.error.value.message || 'Failed to save leave.';
      return;
    }
    emit('save');
  } catch (error) {
    apiError.value = error instanceof Error ? error.message : 'An unexpected error occurred.';
  } finally {
    isSaving.value = false;
  }
};
</script>

<template>
  <UaModal
    :title="isEdit ? (isSeriesEdit ? 'Edit Leave Series' : 'Edit Leave') : 'Add Leave'"
    :loading="isSaving"
    @close="emit('close')"
  >
    <UaAlert v-if="apiError" type="error" @close="apiError = ''">{{ apiError }}</UaAlert>
    <LeaveFormComponent
      v-model="formData"
      :disabled="isSaving || isLoadingEntry || isLoadingSeries"
      :show-repeat="!isEdit || isSeriesEdit"
      :time-zone-id="timeZoneId"
      @validation-change="hasFormValidationError = $event"
    />

    <template #actions>
      <UaBtn variant="outlined" :disabled="isSaving || isLoadingEntry || isLoadingSeries" @click="emit('close')"
        >Cancel</UaBtn
      >
      <UaBtn
        :loading="isSaving"
        :disabled="
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
