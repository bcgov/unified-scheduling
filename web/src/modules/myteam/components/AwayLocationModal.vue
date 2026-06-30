<script setup lang="ts">
import type { AwayLocationRequestDto, AwayLocationResponseDto } from '@/api-access/generated/models';
import {
  postApiUsersUserIdAwayLocations,
  putApiUsersUserIdAwayLocationsAwayLocationId,
} from '@/api-access/generated/away-locations/away-locations';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import UaTextarea from '@/shared/components/UaTextarea.vue';
import type { SelectOption } from '@/types/select';
import { validationMessages } from '@/shared/validation/validationErrors';
import {
  getTodayDateInputValue,
  isDateTimeFullDay,
  toDateInputValue,
  toLocalDateTimeString,
  toOffsetDateTimeString,
  toTimeInputValue,
} from '@/utils/date';
import { computed, ref, watch } from 'vue';
import * as zod from 'zod';
import { useLocationsStore } from '@/stores/LocationsStore';

const props = defineProps<{
  userId: string;
  locations: SelectOption[];
  awayLocation?: AwayLocationResponseDto | null;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'saved'): void;
}>();

const locationsStore = useLocationsStore();

// Timezone is derived from the selected location's timezone
const timezone = computed(() => {
  const locationId = formData.value.locationId;
  if (!locationId) return 'America/Vancouver';
  return locationsStore.entitiesMap[locationId]?.timezone ?? 'America/Vancouver';
});

const isEditMode = computed(() => !!props.awayLocation);
const isSaving = ref(false);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});
const addTime = ref(false);

const modalTitle = computed(() => (isEditMode.value ? 'Edit Away Location' : 'Add Away Location'));
const isFullDay = computed(() => {
  if (!formData.value.startDate || !formData.value.endDate) return true;
  const startDt = toLocalDateTimeString(formData.value.startDate, formData.value.startTime || '');
  const endDt = toLocalDateTimeString(formData.value.endDate, formData.value.endTime || '');
  return isDateTimeFullDay(startDt, endDt);
});

// Internal form state — separate date + time inputs for UX; combined into yyyy-MM-ddTHH:mm on submit
interface FormData {
  locationId: number | undefined;
  startDate: string;
  startTime: string;
  endDate: string;
  endTime: string;
  comment: string | null;
}

const createInitialFormData = (): FormData => ({
  locationId: undefined,
  startDate: getTodayDateInputValue(),
  startTime: '',
  endDate: '',
  endTime: '',
  comment: null,
});

const populateFromAwayLocation = (al: AwayLocationResponseDto): FormData => {
  const startTime = toTimeInputValue(al.startAtUtc) ?? '';
  const endTime = toTimeInputValue(al.endAtUtc) ?? '';
  addTime.value = !isDateTimeFullDay(al.startAtUtc, al.endAtUtc);

  return {
    locationId: al.locationId,
    startDate: toDateInputValue(al.startAtUtc) ?? getTodayDateInputValue(),
    startTime,
    endDate: toDateInputValue(al.endAtUtc ?? undefined) ?? '',
    endTime,
    comment: al.comment ?? null,
  };
};

const formData = ref<FormData>(
  props.awayLocation ? populateFromAwayLocation(props.awayLocation) : createInitialFormData(),
);

const formSchema = computed(() => {
  const timeField = addTime.value ? zod.string().min(1, validationMessages.required) : zod.string();

  return zod
    .object({
      locationId: zod.number({ error: validationMessages.required }),
      startDate: zod.string().min(1, validationMessages.required),
      startTime: timeField,
      endDate: zod.string().min(1, validationMessages.required),
      endTime: timeField,
      comment: zod.string().nullish(),
    })
    .refine((x) => !x.startDate || !x.endDate || x.endDate >= x.startDate, {
      message: 'End Date must be on or after Start Date.',
      path: ['endDate'],
    })
    .refine(
      (x) => {
        if (!x.startTime || !x.endTime) return true;
        if (x.startDate !== x.endDate) return true;
        return x.endTime > x.startTime;
      },
      { message: 'End Time must be after Start Time on the same day.', path: ['endTime'] },
    );
});

const getFieldErrors = (error: zod.ZodError): Record<string, string> => {
  const errors: Record<string, string> = {};
  for (const issue of error.issues) {
    const fieldName = issue.path[0];
    if (typeof fieldName === 'string' && !errors[fieldName]) {
      errors[fieldName] = issue.message;
    }
  }
  return errors;
};

watch(
  () => props.awayLocation,
  (al) => {
    addTime.value = false;
    formData.value = al ? populateFromAwayLocation(al) : createInitialFormData();
    apiError.value = '';
    formErrors.value = {};
  },
);

watch(addTime, (enabled) => {
  if (!enabled) {
    formData.value = { ...formData.value, startTime: '', endTime: '' };
    formErrors.value = { ...formErrors.value, startTime: '', endTime: '' };
  }
});

function validateForm(): AwayLocationRequestDto | null {
  formErrors.value = {};
  const parsed = formSchema.value.safeParse({
    ...formData.value,
    startTime: addTime.value ? formData.value.startTime : '',
    endTime: addTime.value ? formData.value.endTime : '',
    comment: formData.value.comment?.trim() || null,
  });

  if (!parsed.success) {
    formErrors.value = getFieldErrors(parsed.error);
    return null;
  }

  return {
    locationId: parsed.data.locationId,
    startDateTime: toOffsetDateTimeString(
      parsed.data.startDate,
      addTime.value ? parsed.data.startTime : '',
      timezone.value,
    ),
    endDateTime: toOffsetDateTimeString(
      parsed.data.endDate,
      addTime.value ? parsed.data.endTime : '23:59',
      timezone.value,
    ),
    timezone: timezone.value ?? null,
    comment: parsed.data.comment ?? null,
  };
}

const handleSave = async () => {
  const request = validateForm();
  if (!request) return;

  isSaving.value = true;
  apiError.value = '';

  try {
    if (isEditMode.value && props.awayLocation) {
      const { error } = await putApiUsersUserIdAwayLocationsAwayLocationId(
        props.userId,
        props.awayLocation.id!,
        request,
      );

      if (error.value) {
        apiError.value = error.value.message || 'Failed to save away location.';
        return;
      }
    } else {
      const { error } = await postApiUsersUserIdAwayLocations(props.userId, request);
      if (error.value) {
        apiError.value = error.value.message || 'Failed to save away location.';
        return;
      }
    }

    emit('saved');
    emit('close');
  } catch (err: unknown) {
    apiError.value = err instanceof Error ? err.message : 'Failed to save away location.';
  } finally {
    isSaving.value = false;
  }
};
</script>

<template>
  <UaModal :title="modalTitle" :loading="isSaving" @close="emit('close')">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
    </template>

    <UaFormGrid>
      <label class="ua-form-label" for="away-location-select">Location</label>
      <UaSelect
        id="away-location-select"
        v-model="formData.locationId"
        label="Location"
        :items="props.locations"
        :error-messages="formErrors.locationId"
      />

      <label class="ua-form-label">Full / Partial Day</label>
      <div class="ua-form-day-type">
        <span v-if="!formData.startDate || !formData.endDate" class="ua-form-day-type__label">—</span>
        <span v-else-if="isFullDay" class="ua-form-day-type__badge ua-form-day-type__badge--full">Full Day</span>
        <span v-else class="ua-form-day-type__badge ua-form-day-type__badge--partial">Partial Day</span>
      </div>

      <label class="ua-form-label" for="away-location-start-date">{{
        addTime || formData.startTime ? 'Start Date & Time' : 'Start Date'
      }}</label>
      <div class="ua-date-time-row">
        <UaTextField
          id="away-location-start-date"
          v-model="formData.startDate"
          label=""
          type="date"
          :error-messages="formErrors.startDate"
        />
        <UaTextField
          v-if="addTime || formData.startTime"
          id="away-location-start-time"
          v-model="formData.startTime"
          label=""
          type="time"
          :error-messages="formErrors.startTime"
        />
      </div>

      <label class="ua-form-label" for="away-location-end-date">{{
        addTime || formData.endTime ? 'End Date & Time' : 'End Date'
      }}</label>
      <div class="ua-date-time-row">
        <UaTextField
          id="away-location-end-date"
          v-model="formData.endDate"
          label=""
          type="date"
          :error-messages="formErrors.endDate"
        />
        <UaTextField
          v-if="addTime || formData.endTime"
          id="away-location-end-time"
          v-model="formData.endTime"
          label=""
          type="time"
          :error-messages="formErrors.endTime"
        />
      </div>

      <span></span>
      <div class="ua-add-time-row">
        <v-checkbox v-model="addTime" label="Add Time" density="compact" hide-details />
      </div>

      <UaTextarea
        id="away-location-comment"
        label="Comment (optional)"
        :model-value="formData.comment ?? ''"
        @update:model-value="(v: string) => (formData.comment = v)"
      />
    </UaFormGrid>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isSaving" @click="emit('close')">Cancel</UaBtn>
      <UaBtn :loading="isSaving" @click="handleSave">Save</UaBtn>
    </template>
  </UaModal>
</template>

<style scoped>
.ua-date-time-row {
  display: flex;
  gap: 0.75rem;
  align-items: flex-start;
}

.ua-add-time-row {
  margin-top: -0.5rem;
}

.ua-form-day-type {
  margin-bottom: 0.25rem;
}

.ua-form-day-type__label {
  font-weight: 600;
}

.ua-form-day-type__badge {
  display: inline-block;
  padding: 0.1rem 0.5rem;
  border-radius: 4px;
  font-weight: 600;
  font-size: 0.85rem;
}

.ua-form-day-type__badge--full {
  background-color: #e8b5b5;
}

.ua-form-day-type__badge--partial {
  background-color: #aed4bc;
}
</style>
