<script setup lang="ts">
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { computed, onMounted, ref } from 'vue';
import type {
  SchedulingTimeOffEntryResponse as TimeOffEntryResponse,
  SchedulingTimeOffSeriesResponse as TimeOffSeriesResponse,
  UserResponse,
} from '@/api-access/generated/models';
import { resolveSchedulingTimeZoneId } from '@/modules/scheduling/schedulingTimeZone';
import { defaultEndTime, defaultStartTime, parseFormDateTime } from '@/modules/scheduling/schedulingDateTime';
import {
  createTimeOffEntry,
  createTimeOffSeries,
  getTimeOffEntry,
  getTimeOffSeries,
  updateTimeOffEntry,
  updateTimeOffSeries,
} from '@/modules/timeoff/api/timeOffApi';
import { buildTimeOffRequest } from '@/modules/timeoff/timeOffRequest';
import TimeOffFormComponent, { type TimeOffFormData } from '@/modules/timeoff/components/TimeOffForm.vue';
import { useLocationsStore } from '@/stores/LocationsStore';

const props = defineProps<{
  user: UserResponse;
  entry?: TimeOffEntryResponse | null;
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
const loadedSeries = ref<TimeOffSeriesResponse | null>(null);
const loadedEntry = ref<TimeOffEntryResponse | null>(null);
const isEdit = computed(() => props.entry != null);
const isLoadingEntry = ref(false);
const isLoadingSeries = ref(false);
const hasFormValidationError = ref(false);

const buildInitialFormData = (series?: TimeOffSeriesResponse): TimeOffFormData => {
  const entry = loadedEntry.value ?? props.entry;
  const entrySeries =
    entry && 'series' in entry
      ? (entry as TimeOffEntryResponse & { series?: TimeOffSeriesResponse }).series
      : undefined;
  if (!entry) {
    return {
      userIds: [props.user.id],
      leaveType: null,
      date: '',
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
  return {
    userIds: [series?.userIds, isSeriesEdit.value ? entrySeries?.userIds : null, entry.assignedUserIds].find(
      (ids) => ids && ids.length > 0,
    ) ?? [props.user.id],
    leaveType: series?.leaveTypeId ?? entry.leaveTypeId ?? null,
    date: start?.date ?? '',
    startTime: start?.time ?? defaultStartTime,
    endTime: end?.time ?? defaultEndTime,
    repeatMode: isSeriesEdit.value
      ? (series?.recurrenceRule ?? entrySeries?.recurrenceRule)
        ? 'custom'
        : 'never'
      : 'never',
    recurrenceRule: isSeriesEdit.value ? (series?.recurrenceRule ?? entrySeries?.recurrenceRule ?? null) : null,
    notes: series?.notes ?? entry.notes ?? '',
  };
};

const formData = ref<TimeOffFormData>(buildInitialFormData());
onMounted(async () => {
  if (isSeriesEdit.value && props.entry?.timeOffSeriesId != null) {
    isLoadingSeries.value = true;
    try {
      const result = await getTimeOffSeries(props.entry.timeOffSeriesId);
      if (result.error.value) {
        apiError.value = result.error.value.message || 'Failed to load recurring time off.';
        return;
      }
      if (result.data.value) {
        loadedSeries.value = result.data.value;
        formData.value = buildInitialFormData(result.data.value);
      } else {
        apiError.value = 'Time-off series was not found.';
      }
    } catch (error) {
      apiError.value = error instanceof Error ? error.message : 'Failed to load recurring time off.';
    } finally {
      isLoadingSeries.value = false;
    }
    return;
  }

  if (!isEdit.value || props.entry?.id == null) return;

  isLoadingEntry.value = true;
  try {
    const result = await getTimeOffEntry(props.entry.id);
    if (result.error.value) {
      apiError.value = result.error.value.message || 'Failed to load time off.';
      return;
    }
    if (result.data.value) {
      loadedEntry.value = result.data.value;
      formData.value = buildInitialFormData();
    } else {
      apiError.value = 'Time off was not found.';
    }
  } catch (error) {
    apiError.value = error instanceof Error ? error.message : 'Failed to load time off.';
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
  const built = buildTimeOffRequest(formData.value, { locationId: locationId.value, timeZoneId: timeZoneId.value });
  if (built.error !== undefined) {
    apiError.value = built.error;
    return;
  }

  isSaving.value = true;
  try {
    if (isEdit.value && (isSeriesEdit.value ? built.kind !== 'series' : built.kind !== 'entry')) {
      apiError.value = isSeriesEdit.value
        ? 'Choose a recurrence rule to update the entire series.'
        : 'Converting an individual time off into a repeating series is not supported yet.';
      return;
    }
    const result =
      built.kind === 'series'
        ? isSeriesEdit.value && props.entry?.timeOffSeriesId != null
          ? await updateTimeOffSeries(props.entry.timeOffSeriesId, built.request)
          : await createTimeOffSeries(built.request)
        : isEdit.value && props.entry?.id != null
          ? await updateTimeOffEntry(props.entry.id, built.request)
          : await createTimeOffEntry(built.request);
    if (result.error.value) {
      apiError.value = result.error.value.message || 'Failed to save time off.';
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
    :title="isEdit ? (isSeriesEdit ? 'Edit Time Off Series' : 'Edit Leave/Time off') : 'Add Leave/Time off'"
    :loading="isSaving"
    @close="emit('close')"
  >
    <UaAlert v-if="apiError" type="error" @close="apiError = ''">{{ apiError }}</UaAlert>
    <TimeOffFormComponent
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
