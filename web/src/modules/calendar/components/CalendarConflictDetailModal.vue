<script setup lang="ts">
import { mdiPencil } from '@mdi/js';
import { computed, ref, watch } from 'vue';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaAuditDisplay from '@/shared/components/UaAuditDisplay.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { formatCalendarEventDate, formatCalendarEventTimeRange } from '@/utils/date';
import type { CalendarConflict, CalendarConflictEvent } from '../calendarTypes';

const props = defineProps<{
  conflict: CalendarConflict;
  currentEventId: number;
  timeZone?: string;
  loading?: boolean;
  errorMessage?: string;
}>();

const emit = defineEmits<{
  close: [];
  editEvent: [id: number];
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
  return `${event.title} · ${formatCalendarEventDate(event.start, { timeZone: props.timeZone })} · ${formatCalendarEventTimeRange(event.start, event.end, { timeZone: props.timeZone })}`;
}

function overlapTimeLabel() {
  return formatCalendarEventTimeRange(props.conflict.overlapStart, props.conflict.overlapEnd, {
    timeZone: props.timeZone,
  });
}

const overrideLabel = computed(() => (props.conflict.isOverridden ? 'Update override' : 'Override conflict'));
</script>

<template>
  <UaModal title="Conflict summary" tone="error" width="720" :loading="loading" @close="emit('close')">
    <UaAlert v-if="errorMessage" type="error">{{ errorMessage }}</UaAlert>

    <section class="calendar-conflict-detail" aria-label="Calendar conflict">
      <dl class="calendar-conflict-detail__details">
        <dt>Entry:</dt>
        <dd>
          <span>{{ eventLabel(conflictSection.entry) }}</span>
          <button
            type="button"
            class="calendar-conflict-detail__edit"
            :aria-label="`Edit ${conflictSection.entry.title}`"
            @click="emit('editEvent', conflictSection.entry.eventId)"
          >
            <v-icon :icon="mdiPencil" size="18" />
          </button>
        </dd>

        <dt>Overlaps:</dt>
        <dd>
          <span>{{ eventLabel(conflictSection.overlaps) }}</span>
          <button
            type="button"
            class="calendar-conflict-detail__edit"
            :aria-label="`Edit ${conflictSection.overlaps.title}`"
            @click="emit('editEvent', conflictSection.overlaps.eventId)"
          >
            <v-icon :icon="mdiPencil" size="18" />
          </button>
        </dd>

        <dt>From:</dt>
        <dd>{{ overlapTimeLabel() }}</dd>
      </dl>

      <div class="calendar-conflict-detail__override">
        <label for="conflict-override-note">Override notes</label>
        <textarea id="conflict-override-note" v-model="note" rows="3" maxlength="2000" />
        <div v-if="conflict.isOverridden" class="calendar-conflict-detail__audit">
          <strong>Overridden by:</strong>
          <UaAuditDisplay :audit="conflict" :time-zone="timeZone" />
        </div>
        <UaBtn
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
  overflow-wrap: anywhere;
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

.calendar-conflict-detail__override label {
  font-weight: var(--ua-font-weight-semibold);
}

.calendar-conflict-detail__override textarea {
  border: 1px solid var(--ua-border-color);
  border-radius: 4px;
  color: var(--ua-text-primary);
  padding: var(--ua-spacing-sm);
  resize: vertical;
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
