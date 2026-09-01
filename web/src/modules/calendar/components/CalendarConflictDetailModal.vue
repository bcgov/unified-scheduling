<script setup lang="ts">
import { mdiPencil } from '@mdi/js';
import { computed, ref, watch } from 'vue';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaAuditDisplay from '@/shared/components/UaAuditDisplay.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaTextarea from '@/shared/components/UaTextarea.vue';
import { formatCalendarDateTimeRange } from '@/utils/date';
import {
  formatCalendarConflictEventDateTime,
  getCalendarConflictEventTimeZoneLabel,
} from '../calendarConflictFormatting';
import type { CalendarConflict, CalendarConflictEvent } from '../calendarTypes';

const props = defineProps<{
  conflict: CalendarConflict;
  currentEventId: number;
  timeZone?: string;
  loading?: boolean;
  errorMessage?: string;
  canEditEvent?: boolean;
  canOverride?: boolean;
}>();

const emit = defineEmits<{
  close: [];
  editEvent: [event: CalendarConflictEvent];
  override: [note: string];
}>();

const note = ref('');

const conflictSection = computed(() => {
  const currentEventIsOverlap = props.conflict.overlaps.eventId === props.currentEventId;
  return {
    entry: currentEventIsOverlap ? props.conflict.overlaps : props.conflict.entry,
    overlaps: currentEventIsOverlap ? props.conflict.entry : props.conflict.overlaps,
  };
});

watch(
  () => props.conflict,
  (conflict) => {
    note.value = conflict.overrideNote ?? '';
  },
  { immediate: true },
);

function eventLabel(event: CalendarConflictEvent) {
  return `${event.title} · ${formatCalendarConflictEventDateTime(event, props.timeZone)}`;
}

function overlapTimeLabel() {
  return formatCalendarDateTimeRange(props.conflict.overlapStart, props.conflict.overlapEnd, props.timeZone);
}

const overrideLabel = computed(() => (props.conflict.isOverridden ? 'Update override' : 'Override conflict'));

function editEvent(event: CalendarConflictEvent) {
  emit('editEvent', event);
}
</script>

<template>
  <UaModal title="Conflict summary" tone="error" width="720" :loading="loading" @close="emit('close')">
    <UaAlert v-if="errorMessage" type="error">{{ errorMessage }}</UaAlert>

    <section class="calendar-conflict-detail" aria-label="Calendar conflict">
      <p v-if="timeZone" class="calendar-conflict-detail__time-zone">Times shown in {{ timeZone }}</p>
      <dl class="calendar-conflict-detail__details">
        <dt>Entry:</dt>
        <dd>
          <span>
            {{ eventLabel(conflictSection.entry) }}
            <small v-if="getCalendarConflictEventTimeZoneLabel(conflictSection.entry, timeZone)">
              {{ getCalendarConflictEventTimeZoneLabel(conflictSection.entry, timeZone) }}
            </small>
          </span>
          <button
            v-if="canEditEvent"
            type="button"
            class="calendar-conflict-detail__edit"
            :disabled="conflictSection.entry.eventId == null"
            :aria-label="`Edit ${conflictSection.entry.title}`"
            @click="editEvent(conflictSection.entry)"
          >
            <v-icon :icon="mdiPencil" size="18" />
          </button>
        </dd>

        <dt>Overlaps:</dt>
        <dd>
          <span>
            {{ eventLabel(conflictSection.overlaps) }}
            <small v-if="getCalendarConflictEventTimeZoneLabel(conflictSection.overlaps, timeZone)">
              {{ getCalendarConflictEventTimeZoneLabel(conflictSection.overlaps, timeZone) }}
            </small>
          </span>
          <button
            v-if="canEditEvent"
            type="button"
            class="calendar-conflict-detail__edit"
            :disabled="conflictSection.overlaps.eventId == null"
            :aria-label="`Edit ${conflictSection.overlaps.title}`"
            @click="editEvent(conflictSection.overlaps)"
          >
            <v-icon :icon="mdiPencil" size="18" />
          </button>
        </dd>

        <dt>From:</dt>
        <dd>{{ overlapTimeLabel() }}</dd>
      </dl>

      <div class="calendar-conflict-detail__override">
        <UaTextarea
          id="conflict-override-note"
          v-model="note"
          label="Override notes"
          rows="3"
          maxlength="2000"
          :disabled="!canOverride"
        />
        <div v-if="conflict.isOverridden" class="calendar-conflict-detail__audit">
          <strong>Overridden by:</strong>
          <UaAuditDisplay :audit="conflict" :time-zone="timeZone" />
        </div>
        <UaBtn
          v-if="canOverride"
          color="primary"
          variant="flat"
          :disabled="!note.trim() || loading"
          :loading="loading"
          @click="emit('override', note.trim())"
        >
          {{ overrideLabel }}
        </UaBtn>
      </div>
    </section>
  </UaModal>
</template>

<style scoped>
.calendar-conflict-detail {
  display: grid;
  gap: var(--ua-spacing-md);
}

.calendar-conflict-detail__time-zone {
  color: var(--ua-text-secondary);
  margin: 0;
}

.calendar-conflict-detail__details {
  display: grid;
  gap: var(--ua-spacing-sm) var(--ua-spacing-md);
  grid-template-columns: max-content minmax(0, 1fr);
  margin: 0;
}

.calendar-conflict-detail__details dt {
  font-weight: var(--ua-font-weight-semibold);
}

.calendar-conflict-detail__details dd {
  align-items: center;
  display: flex;
  gap: var(--ua-spacing-sm);
  justify-content: space-between;
  margin: 0;
  min-width: 0;
}

.calendar-conflict-detail__details dd span {
  display: grid;
  gap: var(--ua-spacing-xs);
  overflow-wrap: anywhere;
}

.calendar-conflict-detail__details dd small {
  color: var(--ua-text-secondary);
}

.calendar-conflict-detail__edit {
  align-items: center;
  background: transparent;
  border: 0;
  border-radius: 50%;
  color: rgb(var(--v-theme-primary));
  cursor: pointer;
  display: inline-flex;
  flex: 0 0 auto;
  justify-content: center;
  padding: var(--ua-spacing-xs);
}

.calendar-conflict-detail__edit:focus-visible {
  outline: 2px solid rgb(var(--v-theme-primary));
  outline-offset: 2px;
}

.calendar-conflict-detail__override {
  display: grid;
  gap: var(--ua-spacing-sm);
}

.calendar-conflict-detail__audit {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-xs);
}

.calendar-conflict-detail__override :deep(.v-btn) {
  justify-self: end;
}
</style>
