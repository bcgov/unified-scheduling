<script setup lang="ts">
import type { ActingPositionRequestDto, ActingPositionResponseDto } from '@/api-access/generated/models';
import {
  postApiUsersUserIdActingPositions,
  putApiUsersUserIdActingPositionsActingPositionId,
} from '@/api-access/generated/acting-positions/acting-positions';
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
import { useAuthStore } from '@/stores/auth';
import { useLocationsStore } from '@/stores/LocationsStore';

const props = defineProps<{
  userId: string;
  positionTypes: SelectOption[];
  position?: ActingPositionResponseDto | null;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'saved'): void;
}>();

const authStore = useAuthStore();
const locationsStore = useLocationsStore();

const timezone = computed(() =>  authStore.userInfo?.homeLocationId ? locationsStore.entitiesMap[authStore.userInfo.homeLocationId]?.timezone : 'America/Vancouver');

const isEditMode = computed(() => !!props.position);
const isSaving = ref(false);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});
const addTime = ref(false);

const modalTitle = computed(() => (isEditMode.value ? 'Edit Acting Position' : 'Add Acting Position'));
const isFullDay = computed(() => {
  if (!formData.value.startDate || !formData.value.endDate) return true;
  const startDt = toLocalDateTimeString(formData.value.startDate, formData.value.startTime || '');
  const endDt = toLocalDateTimeString(formData.value.endDate, formData.value.endTime || '');
  return isDateTimeFullDay(startDt, endDt);
});

// Internal form state — separate date + time inputs for UX; combined into yyyy-MM-ddTHH:mm on submit
interface FormData {
  positionTypeCode: string | undefined;
  startDate: string;
  startTime: string;
  endDate: string;
  endTime: string;
  comment: string | null;
}

const createInitialFormData = (): FormData => ({
  positionTypeCode: undefined,
  startDate: getTodayDateInputValue(),
  startTime: '',
  endDate: '',
  endTime: '',
  comment: null,
});

const populateFromPosition = (pos: ActingPositionResponseDto): FormData => {
  const startTime = toTimeInputValue(pos.startAtUtc) ?? '';
  const endTime = toTimeInputValue(pos.endAtUtc) ?? '';
  addTime.value = !isDateTimeFullDay(pos.startAtUtc, pos.endAtUtc);

  return {
    positionTypeCode: pos.positionTypeCode,
    startDate: toDateInputValue(pos.startAtUtc) ?? getTodayDateInputValue(),
    startTime,
    endDate: toDateInputValue(pos.endAtUtc ?? undefined) ?? '',
    endTime,
    comment: pos.comment ?? null,
  };
};

const formData = ref<FormData>(props.position ? populateFromPosition(props.position) : createInitialFormData());

const formSchema = computed(() => {
  const timeField = addTime.value ? zod.string().min(1, validationMessages.required) : zod.string();

  return zod
    .object({
      positionTypeCode: zod.string({ error: validationMessages.required }).min(1, validationMessages.required),
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
  () => props.position,
  (pos) => {
    addTime.value = false;
    formData.value = pos ? populateFromPosition(pos) : createInitialFormData();
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

function validateForm(): ActingPositionRequestDto | null {
  formErrors.value = {};
  const parsed = formSchema.value.safeParse({
    ...formData.value,
    startTime: addTime.value ? formData.value.startTime : '',
    endTime: addTime.value ? formData.value.endTime : '',
    comment: formData.value.comment?.trim() || null,
  });

  console.log('validateForm parsed:', parsed);

  if (!parsed.success) {
    formErrors.value = getFieldErrors(parsed.error);
    return null;
  }

  return {
    positionTypeCode: parsed.data.positionTypeCode,
    startDateTime: toOffsetDateTimeString(parsed.data.startDate, addTime.value ? parsed.data.startTime : '', timezone.value),
    endDateTime: toOffsetDateTimeString(parsed.data.endDate, addTime.value ? parsed.data.endTime : '23:59', timezone.value),
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
    if (isEditMode.value && props.position) {
      const { error } = await putApiUsersUserIdActingPositionsActingPositionId(
        props.userId,
        props.position.id!,
        request,
      );

      if (error.value) {
        apiError.value = error.value.message || 'Failed to save acting position.';
        return;
      }
    } else {
      const { error } = await postApiUsersUserIdActingPositions(props.userId, request);
      if (error.value) {
        apiError.value = error.value.message || 'Failed to save acting position.';
        return;
      }
    }

    emit('saved');
    emit('close');
  } catch (err: unknown) {
    apiError.value = err instanceof Error ? err.message : 'Failed to save acting position.';
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
      <label class="ua-form-label" for="acting-position-type">Position Type</label>
      <UaSelect
        id="acting-position-type"
        v-model="formData.positionTypeCode"
        label="Position Type"
        :items="props.positionTypes"
        :disabled="isEditMode"
        :error-messages="formErrors.positionTypeCode"
      />

      <label class="ua-form-label">Full / Partial Day</label>
      <div class="ua-form-day-type">
        <span v-if="!formData.startDate || !formData.endDate" class="ua-form-day-type__label">—</span>
        <span v-else-if="isFullDay" class="ua-form-day-type__badge ua-form-day-type__badge--full">Full Day</span>
        <span v-else class="ua-form-day-type__badge ua-form-day-type__badge--partial">Partial Day</span>
      </div>

      <label class="ua-form-label" for="acting-position-start-date">{{
        addTime || formData.startTime ? 'Start Date & Time' : 'Start Date'
      }}</label>
      <div class="ua-date-time-row">
        <UaTextField
          id="acting-position-start-date"
          v-model="formData.startDate"
          label=""
          type="date"
          :error-messages="formErrors.startDate"
        />
        <UaTextField
          v-if="addTime || formData.startTime"
          id="acting-position-start-time"
          v-model="formData.startTime"
          label=""
          type="time"
          :error-messages="formErrors.startTime"
        />
      </div>

      <label class="ua-form-label" for="acting-position-end-date">{{ addTime || formData.endTime ? 'End Date & Time' : 'End Date' }}</label>
      <div class="ua-date-time-row">
        <UaTextField
          id="acting-position-end-date"
          v-model="formData.endDate"
          label=""
          type="date"
          :error-messages="formErrors.endDate"
        />
        <UaTextField
          v-if="addTime || formData.endTime"
          id="acting-position-end-time"
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
        id="acting-position-comment"
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
