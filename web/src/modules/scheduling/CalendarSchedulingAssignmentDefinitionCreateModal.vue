<script setup lang="ts">
import {
  getApiSchedulingAssignmentDefinitionsId,
  postApiSchedulingAssignmentDefinitions,
  putApiSchedulingAssignmentDefinitionsId,
} from '@/api-access/generated/assignment-definition/assignment-definition';
import { mdiPencil } from '@mdi/js';
import { PostApiSchedulingAssignmentDefinitionsBody } from '@/api-access/generated/assignment-definition/assignment-definition.zod';
import { getApiLookupCodeType } from '@/api-access/generated/lookup/lookup';
import {
  LookupCodeTypes,
  type AssignmentDefinitionResponse,
  type LookupCodeResponse,
} from '@/api-access/generated/models';
import CalendarEventColorPicker from '@/modules/calendar/components/CalendarEventColorPicker.vue';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import { mapToValidationErrors, validationMessages } from '@/shared/validation/validationErrors';
import { useLocationsStore } from '@/stores/LocationsStore';
import type { SelectOption, SelectValue } from '@/types/select';
import { getTodayDateInputValue } from '@/utils/date';
import { DateTime } from 'luxon';
import { computed, onMounted, ref, watch } from 'vue';
import * as zod from 'zod';
import { defaultEndTime, defaultStartTime, normalizeTimeOptionValue, timeOptions } from './calendarSchedulingShiftForm';
import { calendarMatrixColorMap } from './calendarSchedulingColors';
import { isEffectiveDateRangeActive } from './effectiveDateRangeStatus';
import CalendarSchedulingShiftDetailsPanel from './CalendarSchedulingShiftDetailsPanel.vue';
import type { ShiftDetailRow } from './calendarSchedulingShiftDetailTypes';
import { toDefinitionDateInputValue, toDefinitionDateTimeOffset } from './assignmentDefinitionDateHelpers';

const props = defineProps<{
  currentLocationId?: number | null;
  assignmentDefinitionId?: number;
  mode?: 'create' | 'view' | 'edit';
}>();

const emit = defineEmits<{
  close: [];
  saved: [assignmentDefinition: AssignmentDefinitionResponse];
}>();

type AssignmentDefinitionCreateFormData = Partial<zod.infer<typeof PostApiSchedulingAssignmentDefinitionsBody>> & {
  locationId?: SelectValue;
};

const locationsStore = useLocationsStore();
const activeLocationId = computed<number | undefined>(() => {
  if (props.currentLocationId != null) {
    return props.currentLocationId;
  }

  const candidate = locationsStore.selectedLocationId;
  if (candidate === '' || candidate == null) {
    return undefined;
  }

  const parsedLocationId = Number(candidate);
  return Number.isFinite(parsedLocationId) ? parsedLocationId : undefined;
});

const assignmentDefinitionCreateSchema = PostApiSchedulingAssignmentDefinitionsBody.extend({
  locationId: zod.number().min(1, validationMessages.required),
  name: zod.string().trim().min(1, validationMessages.required).max(200, validationMessages.tooLong),
  description: zod.string().trim().max(200, validationMessages.tooLong).optional(),
  assignmentCategoryTypeId: zod.number().min(1, validationMessages.required),
  assignmentSubCategoryTypeId: zod.number().min(1, validationMessages.required),
  color: zod.string().optional(),
  defaultCapacity: zod.number().min(1),
  defaultStartTime: zod.string().min(1, validationMessages.required),
  defaultEndTime: zod.string().min(1, validationMessages.required),
  effectiveDateUtc: zod.string().min(1, validationMessages.required),
}).superRefine((data, ctx) => {
  if (data.defaultStartTime && data.defaultEndTime && data.defaultEndTime <= data.defaultStartTime) {
    ctx.addIssue({ code: 'custom', path: ['defaultEndTime'], message: 'End time must be after start time.' });
  }
});

const createInitialFormData = (): AssignmentDefinitionCreateFormData => ({
  locationId: activeLocationId.value,
  name: '',
  description: '',
  assignmentCategoryTypeId: undefined,
  assignmentSubCategoryTypeId: undefined,
  color: 'blue',
  defaultCapacity: 1,
  defaultStartTime,
  defaultEndTime,
  effectiveDateUtc: getTodayDateInputValue(),
  expiryDateUtc: null,
});

const isSaving = ref(false);
const isLoading = ref(false);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});
const formData = ref<AssignmentDefinitionCreateFormData>(createInitialFormData());
const loadedAssignmentDefinition = ref<AssignmentDefinitionResponse | null>(null);
const modalMode = ref<'create' | 'view' | 'edit'>(props.mode ?? 'create');
const assignmentCategoryTypes = ref<LookupCodeResponse[]>([]);
const assignmentSubCategoryTypes = ref<LookupCodeResponse[]>([]);

const isReadOnly = computed(() => modalMode.value === 'view');
const isEditMode = computed(() => modalMode.value === 'edit');
const modalTitle = computed(() => {
  if (isReadOnly.value) {
    return 'Assignment Type Details';
  }

  if (isEditMode.value) {
    return 'Edit Type Definition';
  }

  return 'Add Type Definition';
});
const locationOptions = computed(() => locationsStore.selectOptions);
const activeDefinitionTimeZoneId = computed(() => {
  const locationId = normalizeNumber(formData.value.locationId) ?? activeLocationId.value;
  return locationId ? locationsStore.entitiesMap[locationId]?.timezone : undefined;
});
const assignmentDefinitionEffectiveContext = computed(() => {
  const date = DateTime.fromISO(String(formData.value.effectiveDateUtc ?? ''), {
    zone: activeDefinitionTimeZoneId.value || 'America/Vancouver',
  });
  return date.isValid ? date : DateTime.now().setZone(activeDefinitionTimeZoneId.value || 'America/Vancouver');
});
const assignmentCategoryOptions = computed<SelectOption[]>(() =>
  assignmentCategoryTypes.value
    .filter((assignmentCategory) => isCategorySelectableForDefinitionDate(assignmentCategory))
    .filter((assignmentCategory) => typeof assignmentCategory.parentCodeTypeId === 'number')
    .map((assignmentCategory) => ({
      code: assignmentCategory.parentCodeTypeId as number,
      description: assignmentCategory.description || assignmentCategory.code || 'Category',
    }))
    .sort((left, right) => left.description.localeCompare(right.description)),
);
const selectedAssignmentCategory = computed(() =>
  assignmentCategoryTypes.value.find(
    (assignmentCategory) => assignmentCategory.parentCodeTypeId === formData.value.assignmentCategoryTypeId,
  ),
);
const assignmentSubCategoryOptions = computed<SelectOption[]>(() => {
  const selectedChildIds = selectedAssignmentCategory.value?.childCodeTypeIds ?? [];
  const selectedChildIdSet = new Set(selectedChildIds);

  return assignmentSubCategoryTypes.value
    .filter((assignmentSubCategory) => isSubCategorySelectableForDefinitionDate(assignmentSubCategory))
    .filter(
      (assignmentSubCategory) => assignmentSubCategory.parentCodeTypeId === formData.value.assignmentCategoryTypeId,
    )
    .filter((assignmentSubCategory) => typeof assignmentSubCategory.childCodeTypeId === 'number')
    .filter((assignmentSubCategory) => selectedChildIdSet.has(assignmentSubCategory.childCodeTypeId as number))
    .map((assignmentSubCategory) => ({
      code: assignmentSubCategory.childCodeTypeId as number,
      description: assignmentSubCategory.description || assignmentSubCategory.code || 'Subcategory',
    }))
    .sort((left, right) => left.description.localeCompare(right.description));
});
const assignmentDefinitionDetailRows = computed<ShiftDetailRow[]>(() => [
  { label: 'Name', value: formData.value.name?.trim() || 'None' },
  { label: 'Description', value: formData.value.description?.trim() || 'None' },
  { label: 'Location', value: formatLocation(formData.value.locationId) },
  {
    label: 'Category',
    value: formatSelectValue(formData.value.assignmentCategoryTypeId, assignmentCategoryOptions.value),
  },
  {
    label: 'Subcategory',
    value: formatSelectValue(formData.value.assignmentSubCategoryTypeId, assignmentSubCategoryOptions.value),
  },
  { label: 'Color', value: formatColorLabel(formData.value.color), color: resolveColorValue(formData.value.color) },
  { label: 'Capacity', value: String(formData.value.defaultCapacity ?? 'None') },
  { label: 'Default Start', value: formatTime(formData.value.defaultStartTime) },
  { label: 'Default End', value: formatTime(formData.value.defaultEndTime) },
  { label: 'Effective Date', value: formatDate(formData.value.effectiveDateUtc) },
  { label: 'Expiry Date', value: formData.value.expiryDateUtc ? formatDate(formData.value.expiryDateUtc) : 'None' },
]);

watch(activeLocationId, (locationId) => {
  if (!formData.value.locationId && locationId) {
    updateField('locationId', locationId);
  }
});

watch(
  () => [props.assignmentDefinitionId, props.mode] as const,
  async () => {
    modalMode.value = props.mode ?? 'create';
    formErrors.value = {};
    apiError.value = '';

    if (props.assignmentDefinitionId) {
      await loadAssignmentDefinition();
    } else {
      loadedAssignmentDefinition.value = null;
      formData.value = createInitialFormData();
    }
  },
);

onMounted(async () => {
  isLoading.value = true;
  try {
    if (locationsStore.entities.length === 0) {
      await locationsStore.getEntities();
    }

    const categoryResult = getApiLookupCodeType(LookupCodeTypes.AssignmentCategoryTypes, {
      options: { immediate: false },
    });
    const subCategoryResult = getApiLookupCodeType(LookupCodeTypes.AssignmentSubCategoryTypes, {
      options: { immediate: false },
    });

    await Promise.all([categoryResult.execute(), subCategoryResult.execute()]);

    if (categoryResult.error.value || subCategoryResult.error.value) {
      apiError.value =
        categoryResult.error.value?.message ||
        subCategoryResult.error.value?.message ||
        'Failed to load assignment definition options.';
      return;
    }

    assignmentCategoryTypes.value = categoryResult.data.value ?? [];
    assignmentSubCategoryTypes.value = subCategoryResult.data.value ?? [];

    if (props.assignmentDefinitionId) {
      await loadAssignmentDefinition();
    }
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'Failed to load assignment definition options.';
  } finally {
    isLoading.value = false;
  }
});

watch(assignmentSubCategoryOptions, (options) => {
  if (
    formData.value.assignmentSubCategoryTypeId &&
    !options.some((option) => option.code === formData.value.assignmentSubCategoryTypeId)
  ) {
    updateField('assignmentSubCategoryTypeId', undefined);
  }
});

function updateField<TKey extends keyof AssignmentDefinitionCreateFormData>(
  key: TKey,
  value: AssignmentDefinitionCreateFormData[TKey],
) {
  formData.value = { ...formData.value, [key]: value };
}

function updateSelectField<TKey extends keyof AssignmentDefinitionCreateFormData>(
  key: TKey,
  value: SelectValue | undefined,
) {
  updateField(key, (value ?? undefined) as AssignmentDefinitionCreateFormData[TKey]);
}

function updateAssignmentCategory(value: SelectValue | undefined) {
  updateSelectField('assignmentCategoryTypeId', value);
  updateField('assignmentSubCategoryTypeId', undefined);
}

function handleClose() {
  if (!isSaving.value) {
    emit('close');
  }
}

function enterEditMode() {
  if (!isSaving.value) {
    modalMode.value = 'edit';
    apiError.value = '';
    formErrors.value = {};
  }
}

function cancelEdit() {
  if (props.assignmentDefinitionId && loadedAssignmentDefinition.value) {
    formData.value = mapAssignmentDefinitionToFormData(loadedAssignmentDefinition.value);
    modalMode.value = 'view';
    apiError.value = '';
    formErrors.value = {};
    return;
  }

  handleClose();
}

function validateForm() {
  formErrors.value = {};
  const candidatePayload = {
    ...formData.value,
    locationId: normalizeNumber(formData.value.locationId),
    description: formData.value.description?.trim() ?? '',
    assignmentCategoryTypeId: normalizeNumber(formData.value.assignmentCategoryTypeId),
    assignmentSubCategoryTypeId: normalizeNumber(formData.value.assignmentSubCategoryTypeId),
    defaultCapacity: Number(formData.value.defaultCapacity),
    effectiveDateUtc: toDefinitionDateTimeOffset(formData.value.effectiveDateUtc, activeDefinitionTimeZoneId.value),
    expiryDateUtc: formData.value.expiryDateUtc
      ? toDefinitionDateTimeOffset(formData.value.expiryDateUtc, activeDefinitionTimeZoneId.value)
      : null,
  };

  const result = assignmentDefinitionCreateSchema.safeParse(candidatePayload);
  if (!result.success) {
    formErrors.value = getFieldErrors(result.error);
    return null;
  }

  return result.data;
}

async function handleSave() {
  if (isReadOnly.value) {
    return;
  }

  const payload = validateForm();
  if (!payload) {
    return;
  }

  isSaving.value = true;
  apiError.value = '';

  try {
    const mutation =
      isEditMode.value && props.assignmentDefinitionId
        ? putApiSchedulingAssignmentDefinitionsId(props.assignmentDefinitionId, payload, {
            options: { immediate: false },
          })
        : postApiSchedulingAssignmentDefinitions(payload, { options: { immediate: false } });
    const { data, error, execute } = mutation;
    await execute();

    if (error.value) {
      formErrors.value = mapToValidationErrors(error.value.data) ?? {};
      apiError.value =
        error.value.message || `Failed to ${isEditMode.value ? 'update' : 'create'} assignment definition.`;
      return;
    }

    if (!data.value) {
      apiError.value = `Failed to ${isEditMode.value ? 'update' : 'create'} assignment definition.`;
      return;
    }

    loadedAssignmentDefinition.value = data.value;
    emit('saved', data.value);
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'An unexpected error occurred.';
  } finally {
    isSaving.value = false;
  }
}

async function loadAssignmentDefinition() {
  if (!props.assignmentDefinitionId) {
    return;
  }

  isLoading.value = true;
  apiError.value = '';

  try {
    const { data, error, execute } = getApiSchedulingAssignmentDefinitionsId(props.assignmentDefinitionId, {
      options: { immediate: false },
    });
    await execute();

    if (error.value) {
      apiError.value = error.value.message || 'Failed to load assignment definition.';
      return;
    }

    if (!data.value) {
      apiError.value = 'Failed to load assignment definition.';
      return;
    }

    loadedAssignmentDefinition.value = data.value;
    formData.value = mapAssignmentDefinitionToFormData(data.value);
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'Failed to load assignment definition.';
  } finally {
    isLoading.value = false;
  }
}

function getFieldErrors(error: zod.ZodError): Record<string, string> {
  const errors: Record<string, string> = {};
  for (const issue of error.issues) {
    const fieldName = issue.path[0];
    if (typeof fieldName === 'string' && !errors[fieldName]) {
      if (issue.code === 'invalid_type' || issue.code === 'invalid_value') {
        errors[fieldName] = validationMessages.required;
        continue;
      }
      errors[fieldName] = issue.message;
    }
  }

  return errors;
}

function normalizeNumber(value: SelectValue | number | undefined | null): number | undefined {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === 'string') {
    const parsedValue = Number(value);
    return Number.isFinite(parsedValue) ? parsedValue : undefined;
  }

  return undefined;
}

function formatLocation(value: SelectValue | undefined) {
  const locationId = normalizeNumber(value);
  if (!locationId) {
    return 'Unknown location';
  }

  const option = locationOptions.value.find((candidate) => Number(candidate.code) === locationId);
  return option?.description || 'Unknown location';
}

function formatSelectValue(value: SelectValue | undefined, options: SelectOption[]) {
  const normalizedValue = normalizeNumber(value);
  if (normalizedValue == null) {
    return 'None';
  }

  const option = options.find((candidate) => Number(candidate.code) === normalizedValue);
  return option?.description || 'None';
}

function resolveColorValue(value?: string | null) {
  if (!value) {
    return undefined;
  }

  return calendarMatrixColorMap[value as keyof typeof calendarMatrixColorMap] ?? value;
}

function formatColorLabel(value?: string | null) {
  if (!value) {
    return 'None';
  }

  return value
    .split(/[-_\s]+/)
    .filter(Boolean)
    .map((part) => `${part.charAt(0).toUpperCase()}${part.slice(1)}`)
    .join(' ');
}

function formatTime(value?: string | null) {
  if (!value) {
    return 'None';
  }

  const option = timeOptions.find((candidate) => candidate.code === normalizeTimeOptionValue(value));
  return option?.description || value;
}

function formatDate(value?: string | null) {
  if (!value) {
    return 'None';
  }

  const parsed = DateTime.fromISO(value);
  return parsed.isValid ? parsed.toLocaleString(DateTime.DATE_FULL) : value;
}

function isCategorySelectableForDefinitionDate(lookup: LookupCodeResponse) {
  if (lookup.parentCodeTypeId === formData.value.assignmentCategoryTypeId) {
    return true;
  }

  return isEffectiveDateRangeActive(lookup, assignmentDefinitionEffectiveContext.value);
}

function isSubCategorySelectableForDefinitionDate(lookup: LookupCodeResponse) {
  if (lookup.childCodeTypeId === formData.value.assignmentSubCategoryTypeId) {
    return true;
  }

  return isEffectiveDateRangeActive(lookup, assignmentDefinitionEffectiveContext.value);
}

function mapAssignmentDefinitionToFormData(
  assignmentDefinition: AssignmentDefinitionResponse,
): AssignmentDefinitionCreateFormData {
  return {
    locationId: assignmentDefinition.locationId,
    name: assignmentDefinition.name ?? '',
    description: assignmentDefinition.description ?? '',
    assignmentCategoryTypeId: assignmentDefinition.assignmentCategoryTypeId,
    assignmentSubCategoryTypeId: assignmentDefinition.assignmentSubCategoryTypeId,
    color: assignmentDefinition.color ?? undefined,
    defaultCapacity: assignmentDefinition.defaultCapacity ?? 1,
    defaultStartTime: normalizeTimeOptionValue(assignmentDefinition.defaultStartTime ?? defaultStartTime),
    defaultEndTime: normalizeTimeOptionValue(assignmentDefinition.defaultEndTime ?? defaultEndTime),
    effectiveDateUtc:
      toDefinitionDateInputValue(assignmentDefinition.effectiveDateUtc, activeDefinitionTimeZoneId.value) ||
      getTodayDateInputValue(),
    expiryDateUtc: toDefinitionDateInputValue(assignmentDefinition.expiryDateUtc, activeDefinitionTimeZoneId.value) || null,
  };
}
</script>

<template>
  <UaModal :title="modalTitle" width="760" :loading="isSaving || isLoading" @close="handleClose">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
    </template>

    <div v-if="isReadOnly" class="assignment-definition-modal__body-actions">
      <UaBtn
        :disabled="isSaving || isLoading"
        :prepend-icon="mdiPencil"
        aria-label="Edit Assignment Definition"
        @click="enterEditMode"
      >
        Edit
      </UaBtn>
    </div>

    <CalendarSchedulingShiftDetailsPanel
      v-if="isReadOnly"
      :detail-rows="assignmentDefinitionDetailRows"
      :is-loading="isLoading"
      aria-label="Assignment Type Details Panel"
    />

    <UaFormGrid v-else>
      <UaTextField
        id="assignment-definition-modal-name"
        label="Name"
        :model-value="formData.name"
        :error-messages="formErrors.name"
        :disabled="isSaving || isLoading"
        @update:model-value="(value: string) => (formData.name = value)"
      />

      <UaTextField
        id="assignment-definition-modal-description"
        label="Description"
        :model-value="formData.description"
        :error-messages="formErrors.description"
        :disabled="isSaving || isLoading"
        @update:model-value="(value: string) => (formData.description = value)"
      />

      <label class="assignment-definition-modal__label" for="assignment-definition-modal-location">Location</label>
      <div class="assignment-definition-modal__field">
        <UaSelect
          id="assignment-definition-modal-location"
          :model-value="formData.locationId"
          :items="locationOptions"
          :error="Boolean(formErrors.locationId)"
          :disabled="isSaving || isLoading"
          @update:model-value="(value: SelectValue | undefined) => updateSelectField('locationId', value)"
        />
        <p v-if="formErrors.locationId" class="assignment-definition-modal__field-error">
          {{ formErrors.locationId }}
        </p>
      </div>

      <label class="assignment-definition-modal__label" for="assignment-definition-modal-category">Category</label>
      <UaSelect
        id="assignment-definition-modal-category"
        :model-value="formData.assignmentCategoryTypeId"
        :items="assignmentCategoryOptions"
        :error="Boolean(formErrors.assignmentCategoryTypeId)"
        :disabled="isSaving || isLoading"
        :loading="isLoading"
        @update:model-value="updateAssignmentCategory"
      />

      <label class="assignment-definition-modal__label" for="assignment-definition-modal-subcategory"
        >Subcategory</label
      >
      <UaSelect
        id="assignment-definition-modal-subcategory"
        :model-value="formData.assignmentSubCategoryTypeId"
        :items="assignmentSubCategoryOptions"
        :error="Boolean(formErrors.assignmentSubCategoryTypeId)"
        :disabled="isSaving || isLoading || !formData.assignmentCategoryTypeId"
        :loading="isLoading"
        @update:model-value="
          (value: SelectValue | undefined) => updateSelectField('assignmentSubCategoryTypeId', value)
        "
      />

      <label class="assignment-definition-modal__label" for="assignment-definition-modal-color">Color</label>
      <CalendarEventColorPicker
        id="assignment-definition-modal-color"
        :model-value="formData.color ?? null"
        :colors="calendarMatrixColorMap"
        :disabled="isSaving || isLoading"
        @update:model-value="(value: string | null) => (formData.color = value ?? undefined)"
      />

      <UaTextField
        id="assignment-definition-modal-capacity"
        label="Capacity"
        type="number"
        :model-value="String(formData.defaultCapacity ?? '')"
        :error-messages="formErrors.defaultCapacity"
        :disabled="isSaving || isLoading"
        @update:model-value="(value: string) => (formData.defaultCapacity = Number(value))"
      />

      <label class="assignment-definition-modal__label" for="assignment-definition-modal-default-start"
        >Default Start</label
      >
      <UaSelect
        id="assignment-definition-modal-default-start"
        :model-value="formData.defaultStartTime"
        :items="timeOptions"
        :error="Boolean(formErrors.defaultStartTime)"
        :disabled="isSaving || isLoading"
        @update:model-value="(value: SelectValue | undefined) => updateSelectField('defaultStartTime', value)"
      />

      <label class="assignment-definition-modal__label" for="assignment-definition-modal-default-end"
        >Default End</label
      >
      <UaSelect
        id="assignment-definition-modal-default-end"
        :model-value="formData.defaultEndTime"
        :items="timeOptions"
        :error="Boolean(formErrors.defaultEndTime)"
        :disabled="isSaving || isLoading"
        @update:model-value="(value: SelectValue | undefined) => updateSelectField('defaultEndTime', value)"
      />

      <UaTextField
        id="assignment-definition-modal-effective-date"
        label="Effective Date"
        type="date"
        :model-value="formData.effectiveDateUtc"
        :error-messages="formErrors.effectiveDateUtc"
        :disabled="isSaving || isLoading"
        @update:model-value="(value: string) => (formData.effectiveDateUtc = value)"
      />

      <UaTextField
        id="assignment-definition-modal-expiry-date"
        label="Expiry Date"
        type="date"
        :model-value="formData.expiryDateUtc ?? ''"
        :error-messages="formErrors.expiryDateUtc"
        :disabled="isSaving || isLoading"
        @update:model-value="(value: string) => (formData.expiryDateUtc = value || null)"
      />
    </UaFormGrid>

    <template v-if="!isReadOnly" #actions>
      <p v-if="isEditMode" class="assignment-definition-modal__save-note">
        Note: any changes to this assignment definition will only apply to future assignments. To change an existing
        assignment, edit it directly.
      </p>
      <UaBtn variant="outlined" :disabled="isSaving" @click="isEditMode ? cancelEdit() : handleClose()">Cancel</UaBtn>
      <UaBtn color="primary" variant="flat" :loading="isSaving" @click="handleSave">Save</UaBtn>
    </template>
  </UaModal>
</template>

<style scoped>
.assignment-definition-modal__label {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
}

.assignment-definition-modal__field {
  display: grid;
  gap: var(--ua-spacing-xs);
}

.assignment-definition-modal__body-actions {
  align-items: center;
  display: flex;
  justify-content: flex-end;
  margin-bottom: var(--ua-spacing-md);
}

.assignment-definition-modal__save-note {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  grid-column: 1 / -1;
  margin: 0;
}

.assignment-definition-modal__field-error {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-sm);
  margin: var(--ua-spacing-xs) 0 0;
}
</style>
