<script setup lang="ts">
import { mdiChevronLeft, mdiChevronRight } from '@mdi/js';
import { computed } from 'vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import type { SelectOption, SelectValue } from '@/types/select';
import CalendarViewTabs from './CalendarViewTabs.vue';
import type { CalendarToolbarAction } from '../registry/calendarActionRegistryTypes';
import type { CalendarViewDefinition } from '../registry/calendarRegistryTypes';
import type { CalendarPeriod } from '../calendarStore';
import { buildCalendarPeriodSelectOptions, isCalendarPeriod } from '../calendarPeriodOptions';

const props = defineProps<{
  views: CalendarViewDefinition[];
  activeViewId: string;
  createActions: CalendarToolbarAction[];
  rangeLabel: string;
  toolbarActions: CalendarToolbarAction[];
  activePeriod: CalendarPeriod;
  periods: readonly CalendarPeriod[];
  isLoading?: boolean;
}>();

const emit = defineEmits<{
  (event: 'update:activeViewId', viewId: string): void;
  (event: 'update:period', period: CalendarPeriod): void;
  (event: 'previous'): void;
  (event: 'next'): void;
  (event: 'today'): void;
  (event: 'triggerToolbarAction', actionId: string): void;
  (event: 'triggerCreateAction', actionId: string): void;
}>();

const periodSelectOptions = computed<SelectOption[]>(() => buildCalendarPeriodSelectOptions(props.periods));

function handlePeriodSelection(value: SelectValue | undefined) {
  if (isCalendarPeriod(value) && props.periods.includes(value)) {
    emit('update:period', value);
  }
}
</script>

<template>
  <div class="calendar-toolbar-shell">
    <div v-if="views.length || createActions.length" class="calendar-toolbar-shell__topbar">
      <CalendarViewTabs
        v-if="views.length"
        class="calendar-toolbar-shell__tabs"
        :views="views"
        :active-view-id="activeViewId"
        @update:active-view-id="emit('update:activeViewId', $event)"
      />

      <div v-if="createActions.length" class="calendar-toolbar-shell__actions">
        <UaBtn
          v-for="action in createActions"
          :key="action.id"
          :disabled="action.disabled || isLoading"
          :variant="action.variant ?? 'outlined'"
          @click="emit('triggerCreateAction', action.id)"
        >
          {{ action.label }}
        </UaBtn>
      </div>
    </div>

    <div v-if="toolbarActions.length" class="calendar-toolbar-shell__secondary-actions">
      <div class="calendar-toolbar-shell__actions">
        <UaBtn
          v-for="action in toolbarActions"
          :key="action.id"
          :disabled="action.disabled || isLoading"
          :variant="action.variant ?? 'outlined'"
          @click="emit('triggerToolbarAction', action.id)"
        >
          {{ action.label }}
        </UaBtn>
      </div>
    </div>

    <div class="calendar-toolbar">
      <div class="calendar-toolbar__group calendar-toolbar__group--range">
        <UaBtn
          class="calendar-toolbar__nav-button"
          variant="outlined"
          :disabled="isLoading"
          aria-label="Previous"
          @click="emit('previous')"
        >
          <v-icon class="calendar-toolbar__nav-icon" :icon="mdiChevronLeft" />
        </UaBtn>
        <div class="calendar-toolbar__range">{{ rangeLabel }}</div>
        <UaBtn
          class="calendar-toolbar__nav-button"
          variant="outlined"
          :disabled="isLoading"
          aria-label="Next"
          @click="emit('next')"
        >
          <v-icon class="calendar-toolbar__nav-icon" :icon="mdiChevronRight" />
        </UaBtn>
      </div>

      <div class="calendar-toolbar__group calendar-toolbar__group--period">
        <UaSelect
          class="calendar-toolbar__period-select"
          label="View"
          :items="periodSelectOptions"
          :model-value="activePeriod"
          @update:model-value="handlePeriodSelection"
        />

        <UaBtn class="calendar-toolbar__today-button" variant="outlined" :disabled="isLoading" @click="emit('today')">
          Today
        </UaBtn>
      </div>
    </div>
  </div>
</template>

<style scoped>
.calendar-toolbar-shell {
  --calendar-toolbar-control-height: 35px;
  display: grid;
}

.calendar-toolbar-shell__topbar {
  align-items: center;
  background: var(--ua-calendar-panel-bg);
  border: 1px solid var(--ua-border-color);
  border-radius: 0;
  display: flex;
  gap: var(--ua-spacing-lg);
  justify-content: space-between;
  padding: var(--ua-spacing-md) var(--ua-spacing-lg);
}

.calendar-toolbar-shell__tabs {
  flex: 1 1 auto;
  margin-bottom: 0;
}

.calendar-toolbar-shell__secondary-actions {
  background: var(--ua-calendar-panel-bg);
  border-inline: 1px solid var(--ua-border-color);
  border-bottom: 1px solid var(--ua-border-color);
  padding: 0 var(--ua-spacing-xl) var(--ua-spacing-md);
}

.calendar-toolbar-shell__actions {
  align-items: center;
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-sm);
  justify-content: flex-end;
}

.calendar-toolbar {
  align-items: center;
  background: rgb(var(--v-theme-surface));
  border: 1px solid var(--ua-border-color);
  border-radius: 0;
  display: grid;
  gap: var(--ua-spacing-md);
  grid-template-columns: minmax(0, 1fr) auto;
  padding: var(--ua-spacing-md) var(--ua-spacing-lg);
}

.calendar-toolbar__group {
  align-items: center;
  display: flex;
  gap: var(--ua-spacing-sm);
}

.calendar-toolbar__group--range {
  align-items: stretch;
  justify-content: center;
}

.calendar-toolbar__range {
  align-items: center;
  background: var(--ua-field-bg);
  display: inline-flex;
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-normal);
  border-radius: var(--ua-border-radius);
  justify-content: center;
  min-height: var(--calendar-toolbar-control-height);
  min-width: 220px;
  padding: 0 var(--ua-spacing-lg);
  text-align: center;
}

.calendar-toolbar__nav-button {
  flex: 0 0 auto;
}

:deep(.calendar-toolbar__nav-button.v-btn) {
  border-radius: var(--ua-border-radius);
  height: var(--calendar-toolbar-control-height);
  min-height: var(--calendar-toolbar-control-height);
  min-width: var(--calendar-toolbar-control-height);
  padding: 0;
  width: var(--calendar-toolbar-control-height);
}

:deep(.calendar-toolbar__nav-button.v-btn),
:deep(.calendar-toolbar__today-button.v-btn),
.calendar-toolbar__range,
.calendar-toolbar-shell :deep(.v-field) {
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
}

.calendar-toolbar-shell :deep(.v-field__field),
.calendar-toolbar-shell :deep(.v-field__input) {
  min-height: var(--calendar-toolbar-control-height);
}

.calendar-toolbar-shell :deep(.v-field__input) {
  align-items: center;
  padding-bottom: 0;
  padding-top: 0;
}

.calendar-toolbar-shell :deep(.v-field__outline) {
  opacity: 0;
}

@media (max-width: 1024px) {
  .calendar-toolbar-shell__topbar {
    border-radius: 0;
    padding: var(--ua-spacing-md) var(--ua-spacing-lg);
  }

  .calendar-toolbar {
    grid-template-columns: 1fr;
  }

  .calendar-toolbar__group {
    justify-content: flex-start;
    flex-wrap: wrap;
  }

  .calendar-toolbar__group--range {
    justify-content: flex-start;
  }

  .calendar-toolbar__group--period {
    justify-content: flex-start;
  }

  .calendar-toolbar__range {
    min-width: 0;
    text-align: left;
  }
}
</style>
