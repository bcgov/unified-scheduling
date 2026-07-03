<script setup lang="ts">
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import { computed, ref, watch } from 'vue';
import {
  getDefaultRRuleEditorModel,
  getRRulePreview,
  modelToRRuleString,
  parseDateLike,
  rruleStringToModel,
  weekdayOptions,
} from './rruleEditor.mapper';
import type {
  EndMode,
  Frequency,
  MonthlyMode,
  NthWeekdayPosition,
  RRuleEditorModel,
  SelectOption,
  Weekday,
} from './rruleEditor.types';
import { validateRRuleEditorModel } from './rruleEditor.validation';

const props = withDefaults(
  defineProps<{
    modelValue?: string | null;
    startDate?: Date | string | null;
    untilDate?: Date | string | null;
    disabled?: boolean;
    readOnly?: boolean;
    useParentGrid?: boolean;
    idPrefix?: string;
    labelWidth?: string;
  }>(),
  {
    modelValue: null,
    startDate: null,
    untilDate: null,
    disabled: false,
    readOnly: false,
    useParentGrid: false,
    idPrefix: 'rrule-editor',
    labelWidth: 'var(--ua-form-label-width)',
  },
);

const emit = defineEmits<{
  'update:modelValue': [value: string | null];
  change: [value: string | null];
  invalid: [reason: string];
}>();

const frequencyOptions: SelectOption<Frequency>[] = [
  { title: 'Daily', value: 'DAILY' },
  { title: 'Weekly', value: 'WEEKLY' },
  { title: 'Monthly', value: 'MONTHLY' },
  { title: 'Yearly', value: 'YEARLY' },
];

const monthlyModeOptions: SelectOption<MonthlyMode>[] = [
  { title: 'Day of month', value: 'monthday' },
  { title: 'Nth weekday', value: 'nth-weekday' },
];

const endModeOptions: SelectOption<EndMode>[] = [
  { title: 'On date', value: 'until' },
  { title: 'After occurrences', value: 'count' },
];

const nthPositionOptions: SelectOption<NthWeekdayPosition>[] = [
  { title: 'First', value: 1 },
  { title: 'Second', value: 2 },
  { title: 'Third', value: 3 },
  { title: 'Fourth', value: 4 },
  { title: 'Last', value: -1 },
];

const weekdayToggleOptions = weekdayOptions.map((weekday) => ({
  ...weekday,
  shortTitle: weekday.title.charAt(0),
}));

const model = ref<RRuleEditorModel>(getDefaultModel());
const unsupportedRule = ref<string | null>(null);
const validationMessage = ref<string | null>(null);
let lastEmittedValue: string | null = null;

const availableEndModeOptions = computed(() => endModeOptions);

const intervalUnit = computed(() => {
  const unitByFrequency: Record<Frequency, string> = {
    DAILY: model.value.interval === 1 ? 'day' : 'days',
    WEEKLY: model.value.interval === 1 ? 'week' : 'weeks',
    MONTHLY: model.value.interval === 1 ? 'month' : 'months',
    YEARLY: model.value.interval === 1 ? 'year' : 'years',
  };

  return unitByFrequency[model.value.frequency];
});

const preview = computed(() => getRRulePreview(model.value, props.startDate));
const readOnlyText = computed(() => {
  if (!props.modelValue) {
    return 'Does not repeat.';
  }

  if (unsupportedRule.value) {
    return props.modelValue;
  }

  return preview.value;
});

const untilDateText = computed({
  get() {
    if (!model.value.until) {
      return '';
    }

    const date = parseDateLike(model.value.until);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  },
  set(value: string) {
    model.value.until = value ? parseDateLike(value) : null;
  },
});

function getDefaultModel() {
  return getDefaultRRuleEditorModel(props.startDate, props.untilDate);
}

function hasUntilDatePreset() {
  return props.untilDate !== null && props.untilDate !== undefined && props.untilDate !== '';
}

function applyPresetUntilDate(nextModel: RRuleEditorModel): RRuleEditorModel {
  if (!hasUntilDatePreset()) {
    return nextModel;
  }

  return {
    ...nextModel,
    endMode: nextModel.endMode,
    until: parseDateLike(props.untilDate),
  };
}

watch(
  () => props.modelValue,
  (value) => {
    if (value === lastEmittedValue) {
      return;
    }

    if (!value) {
      unsupportedRule.value = null;
      validationMessage.value = null;
      model.value = getDefaultModel();
      return;
    }

    const parsed = rruleStringToModel(value, props.startDate);
    if (!parsed.supported || !parsed.model) {
      unsupportedRule.value = value;
      validationMessage.value =
        parsed.reason ?? 'This recurrence rule uses advanced options that cannot be edited here.';
      emit('invalid', validationMessage.value);
      return;
    }

    unsupportedRule.value = null;
    validationMessage.value = null;
    model.value = applyPresetUntilDate(parsed.model);
  },
  { immediate: true },
);

watch(
  () => [props.startDate, props.untilDate] as const,
  ([startDate, untilDate]) => {
    if (!props.modelValue) {
      model.value = getDefaultRRuleEditorModel(startDate, untilDate);
      return;
    }

    if (hasUntilDatePreset() && model.value.endMode === 'until') {
      model.value.until = parseDateLike(untilDate);
    }
  },
);

watch(
  model,
  () => {
    if (unsupportedRule.value) {
      return;
    }

    emitCurrentRule();
  },
  { deep: true },
);

function emitCurrentRule() {
  const validation = validateRRuleEditorModel(model.value);

  if (!validation.valid) {
    validationMessage.value = validation.reason ?? 'This recurrence rule is incomplete.';
    emit('invalid', validationMessage.value);
    return;
  }

  validationMessage.value = null;
  const rrule = modelToRRuleString(model.value, props.startDate);
  lastEmittedValue = rrule;
  emit('update:modelValue', rrule);
  emit('change', rrule);
}

function replaceUnsupportedRule() {
  unsupportedRule.value = null;
  validationMessage.value = null;
  model.value = getDefaultModel();
  emitCurrentRule();
}

function updateNthWeekdayWeekday(weekday: Weekday) {
  model.value.nthWeekday = {
    weekday,
    setPosition: model.value.nthWeekday?.setPosition ?? 1,
  };
}

function updateNthWeekdayPosition(setPosition: NthWeekdayPosition) {
  model.value.nthWeekday = {
    weekday: model.value.nthWeekday?.weekday ?? weekdayOptions[0].value,
    setPosition,
  };
}
</script>

<template>
  <div
    class="rrule-editor"
    :class="{ 'rrule-editor--parent-grid': useParentGrid }"
    :style="{ gridTemplateColumns: `${labelWidth} 1fr` }"
  >
    <p v-if="readOnly" class="rrule-editor__readonly rrule-editor__full-row">{{ readOnlyText }}</p>

    <UaAlert v-else-if="unsupportedRule" class="rrule-editor__full-row" type="warning" :closable="false">
      <div class="rrule-editor__alert">
        <span>This recurrence rule uses advanced options that cannot be edited here.</span>
        <UaBtn size="small" variant="outlined" :disabled="disabled" @click="replaceUnsupportedRule">
          Replace with simple recurrence
        </UaBtn>
      </div>
    </UaAlert>

    <template v-else>
      <label class="rrule-editor__label" :for="`${idPrefix}-frequency`">Frequency</label>
      <v-select
        :id="`${idPrefix}-frequency`"
        v-model="model.frequency"
        :items="frequencyOptions"
        aria-label="Frequency"
        :disabled="disabled"
        hide-details="auto"
      />

      <label class="rrule-editor__label" :for="`${idPrefix}-interval`">Repeats every</label>
      <v-text-field
        :id="`${idPrefix}-interval`"
        v-model.number="model.interval"
        type="number"
        min="1"
        step="1"
        :suffix="intervalUnit"
        aria-label="Repeats every"
        :disabled="disabled"
        hide-details="auto"
      />

      <span v-if="model.frequency === 'WEEKLY'" :id="`${idPrefix}-weekdays-label`" class="rrule-editor__label">
        Days
      </span>
      <div
        v-if="model.frequency === 'WEEKLY'"
        class="rrule-editor__weekdays"
        :aria-labelledby="`${idPrefix}-weekdays-label`"
      >
        <v-btn-toggle
          v-model="model.weekdays"
          class="rrule-editor__weekday-toggle"
          :disabled="disabled"
          multiple
          mandatory="force"
          variant="outlined"
          density="comfortable"
          aria-label="Repeat on days of the week"
        >
          <v-btn
            v-for="weekday in weekdayToggleOptions"
            :key="weekday.value"
            :value="weekday.value"
            :aria-label="weekday.title"
            :title="weekday.title"
            class="rrule-editor__weekday-toggle-button"
          >
            {{ weekday.shortTitle }}
          </v-btn>
        </v-btn-toggle>
      </div>

      <span v-if="model.frequency === 'MONTHLY'" :id="`${idPrefix}-monthly-pattern-label`" class="rrule-editor__label">
        Monthly pattern
      </span>
      <div v-if="model.frequency === 'MONTHLY'" class="rrule-editor__section">
        <v-radio-group
          v-model="model.monthlyMode"
          :aria-labelledby="`${idPrefix}-monthly-pattern-label`"
          :disabled="disabled"
          hide-details="auto"
        >
          <v-radio
            v-for="option in monthlyModeOptions"
            :key="option.value"
            :label="option.title"
            :value="option.value"
          />
        </v-radio-group>
      </div>

      <template v-if="model.frequency === 'MONTHLY' && model.monthlyMode === 'monthday'">
        <label class="rrule-editor__label" :for="`${idPrefix}-month-day`">Day of month</label>
        <v-text-field
          :id="`${idPrefix}-month-day`"
          v-model.number="model.monthDay"
          type="number"
          min="1"
          max="31"
          step="1"
          aria-label="Day of month"
          :disabled="disabled"
          hide-details="auto"
        />
      </template>

      <template v-if="model.frequency === 'MONTHLY' && model.monthlyMode === 'nth-weekday'">
        <label class="rrule-editor__label" :for="`${idPrefix}-nth-position`">Position</label>
        <v-select
          :id="`${idPrefix}-nth-position`"
          :model-value="model.nthWeekday?.setPosition"
          :items="nthPositionOptions"
          aria-label="Position"
          :disabled="disabled"
          hide-details="auto"
          @update:model-value="updateNthWeekdayPosition"
        />

        <label class="rrule-editor__label" :for="`${idPrefix}-nth-weekday`">Weekday</label>
        <v-select
          :id="`${idPrefix}-nth-weekday`"
          :model-value="model.nthWeekday?.weekday"
          :items="weekdayOptions"
          aria-label="Weekday"
          :disabled="disabled"
          hide-details="auto"
          @update:model-value="updateNthWeekdayWeekday"
        />
      </template>

      <UaAlert v-if="model.frequency === 'YEARLY'" class="rrule-editor__full-row" type="info" :closable="false">
        Yearly recurrence uses the month and day from the start date.
      </UaAlert>

      <span :id="`${idPrefix}-ends-label`" class="rrule-editor__label">Ends</span>
      <div class="rrule-editor__section">
        <v-radio-group
          v-model="model.endMode"
          :aria-labelledby="`${idPrefix}-ends-label`"
          :disabled="disabled"
          hide-details="auto"
        >
          <v-radio
            v-for="option in availableEndModeOptions"
            :key="option.value"
            :label="option.title"
            :value="option.value"
          />
        </v-radio-group>
      </div>

      <template v-if="model.endMode === 'until'">
        <label class="rrule-editor__label" :for="`${idPrefix}-until`">End date</label>
        <v-text-field
          :id="`${idPrefix}-until`"
          v-model="untilDateText"
          type="date"
          aria-label="End date"
          :disabled="disabled"
          hide-details="auto"
        />
      </template>

      <template v-if="model.endMode === 'count'">
        <label class="rrule-editor__label" :for="`${idPrefix}-count`">Recurrences</label>
        <v-text-field
          :id="`${idPrefix}-count`"
          v-model.number="model.count"
          type="number"
          min="1"
          step="1"
          aria-label="Recurrences"
          :disabled="disabled"
          hide-details="auto"
        />
      </template>

      <UaAlert v-if="validationMessage" class="rrule-editor__full-row" type="error" :closable="false">
        {{ validationMessage }}
      </UaAlert>

      <span aria-hidden="true"></span>
      <p class="rrule-editor__preview" aria-live="polite">{{ preview }}</p>
    </template>
  </div>
</template>

<style scoped>
.rrule-editor {
  display: grid;
  gap: var(--ua-spacing-md);
  grid-template-columns: var(--ua-form-label-width) 1fr;
}

.rrule-editor--parent-grid {
  display: contents;
}

.rrule-editor__full-row {
  grid-column: 1 / -1;
}

.rrule-editor__label {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
}

.rrule-editor__section {
  display: grid;
  gap: var(--ua-spacing-sm);
}

.rrule-editor__weekdays {
  inline-size: 100%;
}

.rrule-editor__weekday-toggle {
  display: flex;
  gap: var(--ua-spacing-sm);
  height: auto;
  inline-size: 100%;
  justify-content: space-between;
}

.rrule-editor__weekday-toggle-button {
  flex: 1 1 0;
  min-inline-size: 0;
  padding: var(--ua-spacing-sm);
  border: 1px solid var(--ua-border-color);
}

.rrule-editor__alert {
  align-items: center;
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-sm);
  justify-content: space-between;
}

.rrule-editor__preview {
  color: rgba(var(--v-theme-on-surface), var(--v-medium-emphasis-opacity));
  margin: 0;
}

.rrule-editor__readonly {
  margin: 0;
}

@media (max-width: 640px) {
  .rrule-editor {
    grid-template-columns: 1fr !important;
  }
}
</style>
