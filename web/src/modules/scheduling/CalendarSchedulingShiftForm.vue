<script setup lang="ts">
import RRuleEditor from '@/components/recurrence/RRuleEditor.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import UaTextarea from '@/shared/components/UaTextarea.vue';
import type { SelectOption, SelectValue } from '@/types/select';
import { mdiDelete } from '@mdi/js';
import { computed, watch } from 'vue';
import { publishOptions, repeatOptions, type ShiftResourceFormData } from './calendarSchedulingShiftForm';
import { timeOptions } from './schedulingDateTime';
import { normalizeSchedulingLifecycleStatus } from './schedulingLifecycle';
import { parsePositiveInteger } from './calendarSchedulingShiftIds';
import { parseStringArray } from './calendarSchedulingLinkMappers';

const props = withDefaults(
  defineProps<{
    modelValue: ShiftResourceFormData;
    formErrors?: Record<string, string>;
    disabled?: boolean;
    showRecurrence?: boolean;
    locationOptions: SelectOption[];
    employeeOptions: SelectOption[];
    isLoadingUsers?: boolean;
    assignmentEntryOptions?: SelectOption[];
    assignmentSeriesOptions?: SelectOption[];
    assignmentWarning?: string;
    isLoadingAssignments?: boolean;
    showSeriesAssignment?: boolean;
    idPrefix?: string;
  }>(),
  {
    formErrors: () => ({}),
    disabled: false,
    showRecurrence: true,
    locationOptions: () => [],
    isLoadingUsers: false,
    assignmentEntryOptions: () => [],
    assignmentSeriesOptions: () => [],
    assignmentWarning: '',
    isLoadingAssignments: false,
    showSeriesAssignment: false,
    idPrefix: 'shift-form',
  },
);

const emit = defineEmits<{
  'update:modelValue': [value: ShiftResourceFormData];
  recurrenceChange: [value: string | null];
  recurrenceInvalid: [reason: string];
}>();

const formData = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value),
});

const isDraftStatus = computed(() => normalizeSchedulingLifecycleStatus(formData.value.statusTypeCode) === 'draft');
const showAssignmentEntryLinks = computed(() => !props.showSeriesAssignment);
const selectedAssignmentEntryIds = computed(
  () =>
    new Set(
      (formData.value.assignmentEntryLinks ?? []).flatMap((link) => parsePositiveInteger(link.assignmentEntryId)),
    ),
);
const selectedAssignmentSeriesIds = computed(
  () =>
    new Set(
      (formData.value.assignmentSeriesLinks ?? []).flatMap((link) => parsePositiveInteger(link.assignmentSeriesId)),
    ),
);
const availableAssignmentEntryOptions = computed(() =>
  props.assignmentEntryOptions.filter((option) => !selectedAssignmentEntryIds.value.has(Number(option.code))),
);
const availableAssignmentSeriesOptions = computed(() =>
  props.assignmentSeriesOptions.filter((option) => !selectedAssignmentSeriesIds.value.has(Number(option.code))),
);
const currentShiftUserOptions = computed(() => {
  const selectedUserIds = new Set(
    (formData.value.userIds ?? []).filter((userId): userId is string => typeof userId === 'string'),
  );

  return props.employeeOptions.filter((option) => typeof option.code === 'string' && selectedUserIds.has(option.code));
});
const locationOptionsWithSelected = computed(() => {
  const locationId = parsePositiveInteger(formData.value.locationId);
  if (!locationId || props.locationOptions.some((option) => Number(option.code) === locationId)) {
    return props.locationOptions;
  }

  return [{ code: locationId, description: 'Unknown location' }, ...props.locationOptions];
});

watch(
  () => formData.value.userIds,
  () => {
    pruneLinkUsersToCurrentShiftUsers();
  },
);

function updateField<TKey extends keyof ShiftResourceFormData>(key: TKey, value: ShiftResourceFormData[TKey]) {
  formData.value = {
    ...formData.value,
    [key]: value,
  };
}

function updateSelectField<TKey extends keyof ShiftResourceFormData>(key: TKey, value: SelectValue | undefined) {
  updateField(key, (value ?? null) as ShiftResourceFormData[TKey]);
}

function updateLocation(value: SelectValue | undefined) {
  const locationId = parsePositiveInteger(value);
  if (locationId === parsePositiveInteger(formData.value.locationId)) {
    return;
  }

  formData.value = {
    ...formData.value,
    locationId,
    userIds: [],
    assignmentEntryLinks: [],
    assignmentSeriesLinks: [],
  };
}

function updateSelectedAssignmentEntry(value: SelectValue | undefined) {
  const assignmentEntryId = parsePositiveInteger(value);
  if (!assignmentEntryId || selectedAssignmentEntryIds.value.has(assignmentEntryId)) {
    return;
  }

  const nextLinks = [
    ...(formData.value.assignmentEntryLinks ?? []),
    {
      assignmentEntryId,
      assignedUserIds: getCurrentShiftUserIds(),
    },
  ];
  formData.value = {
    ...formData.value,
    assignmentEntryLinks: nextLinks,
  };
}

function updateSelectedAssignmentSeries(value: SelectValue | undefined) {
  const assignmentSeriesId = parsePositiveInteger(value);
  if (!assignmentSeriesId || selectedAssignmentSeriesIds.value.has(assignmentSeriesId)) {
    return;
  }

  const nextLinks = [
    ...(formData.value.assignmentSeriesLinks ?? []),
    {
      assignmentSeriesId,
      assignedUserIds: getCurrentShiftUserIds(),
    },
  ];
  formData.value = {
    ...formData.value,
    assignmentSeriesLinks: nextLinks,
  };
}

function removeAssignmentEntryLink(index: number) {
  const nextLinks = [...(formData.value.assignmentEntryLinks ?? [])];
  nextLinks.splice(index, 1);
  formData.value = {
    ...formData.value,
    assignmentEntryLinks: nextLinks,
  };
}

function removeAssignmentSeriesLink(index: number) {
  const nextLinks = [...(formData.value.assignmentSeriesLinks ?? [])];
  nextLinks.splice(index, 1);
  formData.value = {
    ...formData.value,
    assignmentSeriesLinks: nextLinks,
  };
}

function updateAssignmentEntryLinkUsers(index: number, value: SelectValue | undefined) {
  const nextLinks = [...(formData.value.assignmentEntryLinks ?? [])];
  const link = nextLinks[index];
  if (!link) {
    return;
  }

  nextLinks[index] = { ...link, assignedUserIds: parseStringArray(value) };
  updateField('assignmentEntryLinks', nextLinks);
}

function updateAssignmentSeriesLinkUsers(index: number, value: SelectValue | undefined) {
  const nextLinks = [...(formData.value.assignmentSeriesLinks ?? [])];
  const link = nextLinks[index];
  if (!link) {
    return;
  }

  nextLinks[index] = { ...link, assignedUserIds: parseStringArray(value) };
  updateField('assignmentSeriesLinks', nextLinks);
}

function pruneLinkUsersToCurrentShiftUsers() {
  const currentShiftUserIds = new Set(getCurrentShiftUserIds());
  const nextEntryLinks = (formData.value.assignmentEntryLinks ?? []).map((link) => ({
    ...link,
    assignedUserIds: (link.assignedUserIds ?? []).filter((userId) => currentShiftUserIds.has(userId)),
  }));
  const nextSeriesLinks = (formData.value.assignmentSeriesLinks ?? []).map((link) => ({
    ...link,
    assignedUserIds: (link.assignedUserIds ?? []).filter((userId) => currentShiftUserIds.has(userId)),
  }));

  if (JSON.stringify(nextEntryLinks) !== JSON.stringify(formData.value.assignmentEntryLinks ?? [])) {
    updateField('assignmentEntryLinks', nextEntryLinks);
  }

  if (JSON.stringify(nextSeriesLinks) !== JSON.stringify(formData.value.assignmentSeriesLinks ?? [])) {
    updateField('assignmentSeriesLinks', nextSeriesLinks);
  }
}

function formatAssignmentEntryLinkTitle(assignmentEntryId?: number) {
  return formatAssignmentLinkTitle(props.assignmentEntryOptions, assignmentEntryId, 'Assignment');
}

function formatAssignmentSeriesLinkTitle(assignmentSeriesId?: number | null) {
  return formatAssignmentLinkTitle(props.assignmentSeriesOptions, assignmentSeriesId, 'Assignment series');
}

function getAssignmentEntryLinkUserError(index: number) {
  return (
    props.formErrors[`assignmentEntryLinks.${index}.assignedUserIds`] ||
    props.formErrors[`assignmentEntryLinks.${index}.userIds`] ||
    props.formErrors.assignmentEntryLinks ||
    ''
  );
}

function getAssignmentSeriesLinkUserError(index: number) {
  return (
    props.formErrors[`assignmentSeriesLinks.${index}.assignedUserIds`] ||
    props.formErrors[`assignmentSeriesLinks.${index}.userIds`] ||
    props.formErrors.assignmentSeriesLinks ||
    ''
  );
}

function formatAssignmentLinkTitle(options: SelectOption[], id: unknown, fallback: string) {
  const parsedId = parsePositiveInteger(id);
  const option = options.find((candidate) => Number(candidate.code) === parsedId);
  return option?.description || (parsedId ? `${fallback} ${parsedId}` : fallback);
}

function getCurrentShiftUserIds() {
  return (formData.value.userIds ?? []).filter((userId): userId is string => typeof userId === 'string');
}

function handleRecurrenceChange(value: string | null) {
  updateField('recurrenceRule', value);
  emit('recurrenceChange', value);
}
</script>

<template>
  <UaFormGrid label-width="150px">
    <UaTextField
      :id="`${idPrefix}-date`"
      label="Date"
      type="date"
      :model-value="formData.date"
      :error-messages="formErrors.date"
      :disabled="disabled"
      @update:model-value="(value: string) => updateField('date', value)"
    />

    <span :id="`${idPrefix}-time-label`" class="shift-form__label">Time</span>
    <div class="shift-form__time-fields" :aria-labelledby="`${idPrefix}-time-label`">
      <div class="shift-form__time-field">
        <span class="shift-form__time-caption">Start</span>
        <UaSelect
          :model-value="formData.startTime"
          aria-label="Start Time"
          :items="timeOptions"
          :error="Boolean(formErrors.startTime)"
          :disabled="disabled"
          @update:model-value="(value: SelectValue | undefined) => updateSelectField('startTime', value)"
        />
        <p v-if="formErrors.startTime" class="shift-form__field-error">
          {{ formErrors.startTime }}
        </p>
      </div>
      <div class="shift-form__time-field">
        <span class="shift-form__time-caption">End</span>
        <UaSelect
          :model-value="formData.endTime"
          aria-label="End Time"
          :items="timeOptions"
          :error="Boolean(formErrors.endTime)"
          :disabled="disabled"
          @update:model-value="(value: SelectValue | undefined) => updateSelectField('endTime', value)"
        />
        <p v-if="formErrors.endTime" class="shift-form__field-error">
          {{ formErrors.endTime }}
        </p>
      </div>
    </div>

    <template v-if="props.showRecurrence">
      <label class="shift-form__label" :for="`${idPrefix}-repeat`">Repeat</label>

      <div class="shift-form__repeat-field">
        <UaSelect
          :id="`${idPrefix}-repeat`"
          :model-value="formData.repeatMode"
          aria-label="Repeat"
          :items="repeatOptions"
          :error="Boolean(formErrors.repeatMode)"
          :disabled="disabled"
          @update:model-value="(value: SelectValue | undefined) => updateSelectField('repeatMode', value)"
        />
        <p v-if="formErrors.repeatMode" class="shift-form__field-error">
          {{ formErrors.repeatMode }}
        </p>
      </div>

      <RRuleEditor
        v-if="formData.repeatMode === 'custom'"
        :id-prefix="`${idPrefix}-recurrence`"
        :model-value="formData.recurrenceRule ?? null"
        :start-date="formData.date ?? null"
        :disabled="disabled"
        use-parent-grid
        @update:model-value="handleRecurrenceChange"
        @change="handleRecurrenceChange"
        @invalid="(reason: string) => emit('recurrenceInvalid', reason)"
      />
      <template v-else>
        <span aria-hidden="true"></span>
        <p class="shift-form__helper-text">This shift will not repeat.</p>
      </template>

      <template v-if="formErrors.recurrenceRule">
        <span aria-hidden="true"></span>
        <p class="shift-form__field-error">
          {{ formErrors.recurrenceRule }}
        </p>
      </template>
    </template>

    <label class="shift-form__label" :for="`${idPrefix}-location`">Location</label>
    <div class="shift-form__location-field">
      <UaSelect
        :id="`${idPrefix}-location`"
        :model-value="formData.locationId"
        aria-label="Location"
        :items="locationOptionsWithSelected"
        :error="Boolean(formErrors.locationId)"
        :disabled="disabled"
        @update:model-value="updateLocation"
      />
      <p v-if="formErrors.locationId" class="shift-form__field-error">
        {{ formErrors.locationId }}
      </p>
    </div>

    <label class="shift-form__label" :for="`${idPrefix}-employee`">Employee</label>
    <div class="shift-form__employee-field">
      <UaSelect
        :id="`${idPrefix}-employee`"
        :model-value="formData.userIds"
        aria-label="Employee"
        :items="employeeOptions"
        :error="Boolean(formErrors.userIds)"
        :disabled="disabled || isLoadingUsers"
        :loading="isLoadingUsers"
        multiple
        chips
        closable-chips
        clearable
        @update:model-value="(value: SelectValue | undefined) => updateSelectField('userIds', value)"
      />
      <p v-if="formErrors.userIds" class="shift-form__field-error">
        {{ formErrors.userIds }}
      </p>
    </div>

    <template v-if="showAssignmentEntryLinks">
      <label class="shift-form__label" :for="`${idPrefix}-assignment`">Assignment(s)</label>
      <div class="shift-form__assignment-field">
        <UaSelect
          :id="`${idPrefix}-assignment`"
          :model-value="null"
          :items="availableAssignmentEntryOptions"
          :error="Boolean(formErrors.assignmentEntryLinks)"
          :disabled="disabled || isLoadingAssignments"
          :loading="isLoadingAssignments"
          clearable
          @update:model-value="updateSelectedAssignmentEntry"
        />
        <p class="shift-form__helper-text">Select an assignment to add it as a linked assignment for this shift.</p>
        <p v-if="formErrors.assignmentEntryLinks" class="shift-form__field-error">
          {{ formErrors.assignmentEntryLinks }}
        </p>
      </div>

      <template v-if="(formData.assignmentEntryLinks ?? []).length">
        <span aria-hidden="true"></span>
        <h3 class="shift-form__section-heading">Linked assignments</h3>
      </template>

      <template
        v-for="(link, index) in formData.assignmentEntryLinks ?? []"
        :key="`assignment-entry-link-${link.assignmentEntryId}`"
      >
        <span aria-hidden="true"></span>
        <section class="shift-form__link-section">
          <div class="shift-form__link-section-header">
            <h3 class="shift-form__link-section-title">Assignment {{ index + 1 }}</h3>
            <UaBtn
              v-if="!disabled"
              variant="text"
              :aria-label="`Remove Assignment ${index + 1}`"
              @click="removeAssignmentEntryLink(index)"
            >
              <v-icon :icon="mdiDelete" size="18" />
            </UaBtn>
          </div>
          <p class="shift-form__link-section-summary">{{ formatAssignmentEntryLinkTitle(link.assignmentEntryId) }}</p>
          <p class="shift-form__helper-text">Users are limited to employees currently selected on this shift.</p>
          <UaSelect
            :model-value="link.assignedUserIds"
            :items="currentShiftUserOptions"
            label="Users"
            multiple
            chips
            closable-chips
            :disabled="disabled"
            :error="Boolean(getAssignmentEntryLinkUserError(index))"
            @update:model-value="(value: SelectValue | undefined) => updateAssignmentEntryLinkUsers(index, value)"
          />
          <p v-if="getAssignmentEntryLinkUserError(index)" class="shift-form__field-error">
            At least one user is required.
          </p>
        </section>
      </template>
    </template>

    <template v-if="showSeriesAssignment">
      <label class="shift-form__label" :for="`${idPrefix}-series-assignment`">Series Assignments</label>
      <div class="shift-form__assignment-field">
        <UaSelect
          :id="`${idPrefix}-series-assignment`"
          :model-value="null"
          :items="availableAssignmentSeriesOptions"
          :error="Boolean(formErrors.assignmentSeriesLinks)"
          :disabled="disabled || isLoadingAssignments"
          :loading="isLoadingAssignments"
          clearable
          @update:model-value="updateSelectedAssignmentSeries"
        />
        <p class="shift-form__helper-text">
          Select a recurring assignment to add it as a linked recurring assignment for this shift series.
        </p>
        <p v-if="formErrors.assignmentSeriesLinks" class="shift-form__field-error">
          {{ formErrors.assignmentSeriesLinks }}
        </p>
      </div>
    </template>

    <template v-if="showSeriesAssignment">
      <template v-if="(formData.assignmentSeriesLinks ?? []).length">
        <span aria-hidden="true"></span>
        <h3 class="shift-form__section-heading">Linked recurring assignments</h3>
      </template>

      <template
        v-for="(link, index) in formData.assignmentSeriesLinks ?? []"
        :key="`assignment-series-link-${link.assignmentSeriesId}`"
      >
        <span aria-hidden="true"></span>
        <section class="shift-form__link-section">
          <div class="shift-form__link-section-header">
            <h3 class="shift-form__link-section-title">Recurring Assignment {{ index + 1 }}</h3>
            <UaBtn
              v-if="!disabled"
              variant="text"
              :aria-label="`Remove Recurring Assignment ${index + 1}`"
              @click="removeAssignmentSeriesLink(index)"
            >
              <v-icon :icon="mdiDelete" size="18" />
            </UaBtn>
          </div>
          <p class="shift-form__link-section-summary">
            {{ formatAssignmentSeriesLinkTitle(link.assignmentSeriesId) }}
          </p>
          <p class="shift-form__helper-text">Users are limited to employees currently selected on this shift series.</p>
          <UaSelect
            :model-value="link.assignedUserIds"
            :items="currentShiftUserOptions"
            label="Users"
            multiple
            chips
            closable-chips
            :disabled="disabled"
            :error="Boolean(getAssignmentSeriesLinkUserError(index))"
            @update:model-value="(value: SelectValue | undefined) => updateAssignmentSeriesLinkUsers(index, value)"
          />
          <p v-if="getAssignmentSeriesLinkUserError(index)" class="shift-form__field-error">
            At least one user is required.
          </p>
        </section>
      </template>
    </template>

    <template v-if="assignmentWarning">
      <span aria-hidden="true"></span>
      <p class="shift-form__warning-text">
        {{ assignmentWarning }}
      </p>
    </template>

    <UaTextField
      :id="`${idPrefix}-training`"
      label="Training"
      :model-value="formData.trainingLabel"
      placeholder=""
      :disabled="true"
      @update:model-value="(value: string) => updateField('trainingLabel', value)"
    />

    <label v-if="isDraftStatus" class="shift-form__label" :for="`${idPrefix}-publish`">Publish</label>
    <div v-if="isDraftStatus" class="shift-form__status-field">
      <UaSelect
        :id="`${idPrefix}-publish`"
        :model-value="formData.publish"
        aria-label="Publish"
        :items="publishOptions"
        :error="Boolean(formErrors.publish)"
        :disabled="disabled"
        @update:model-value="(value: SelectValue | undefined) => updateSelectField('publish', value)"
      />
      <p v-if="formErrors.publish" class="shift-form__field-error">
        {{ formErrors.publish }}
      </p>
    </div>

    <UaTextarea
      :id="`${idPrefix}-notes`"
      label="Notes"
      :model-value="formData.notes ?? ''"
      :disabled="disabled"
      :error-messages="formErrors.notes"
      rows="3"
      counter="200"
      @update:model-value="(value: string) => updateField('notes', value)"
    />
  </UaFormGrid>
</template>

<style scoped>
.shift-form__label {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
}

.shift-form__time-fields {
  display: grid;
  gap: var(--ua-spacing-md);
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.shift-form__time-field,
.shift-form__repeat-field,
.shift-form__status-field,
.shift-form__location-field,
.shift-form__employee-field,
.shift-form__assignment-field {
  display: grid;
  gap: var(--ua-spacing-xs);
}

.shift-form__repeat-field {
  gap: var(--ua-spacing-md);
}

.shift-form__time-caption {
  color: var(--ua-text-secondary);
  display: block;
  font-size: var(--ua-font-size-sm);
}

.shift-form__helper-text {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
}

.shift-form__section-heading {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-bold);
  margin: 0;
}

.shift-form__field-error {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-sm);
  margin: var(--ua-spacing-xs) 0 0;
}

.shift-form__warning-text {
  color: rgb(var(--v-theme-warning));
  font-size: var(--ua-font-size-sm);
  margin: 0;
}

.shift-form__link-section {
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  display: grid;
  gap: var(--ua-spacing-sm);
  padding: var(--ua-spacing-md);
}

.shift-form__link-section-header {
  align-items: center;
  display: flex;
  justify-content: space-between;
}

.shift-form__link-section-title {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-bold);
  margin: 0;
}

.shift-form__link-section-summary {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
}

@media (max-width: 640px) {
  .shift-form__time-fields {
    grid-template-columns: 1fr;
  }
}
</style>
