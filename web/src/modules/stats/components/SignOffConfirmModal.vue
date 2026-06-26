<script setup lang="ts">
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { computed } from 'vue';
import type { DashboardEntryResponse } from '@/api-access/generated/models';
import { EntryStatus } from '../constants';

const props = defineProps<{
  selectedEntries: DashboardEntryResponse[];
  groupName: string;
}>();

const emit = defineEmits<{
  confirm: [entryIds: number[]];
  close: [];
}>();

const eligibleEntries = computed(() =>
  props.selectedEntries.filter(
    (e) => e.id != null && (e.status === EntryStatus.Draft || e.status === EntryStatus.Submitted),
  ),
);

function confirm() {
  emit(
    'confirm',
    eligibleEntries.value.map((e) => e.id!),
  );
}
</script>

<template>
  <UaModal title="Confirm Sign Off" width="520" @close="emit('close')">
    <div class="signoff-modal">
      <p class="signoff-description">
        Sign off <strong>{{ groupName }}</strong> entries for:
      </p>

      <div class="targets-list">
        <div v-for="entry in eligibleEntries" :key="entry.id" class="target-row">
          <div class="target-info">
            <span class="target-name">{{ entry.employeeName }}</span>
            <span class="target-date">{{ entry.date }}</span>
          </div>
          <span class="target-area">{{ entry.workArea }}</span>
        </div>
        <div v-if="eligibleEntries.length === 0" class="no-eligible">No entries available to sign off.</div>
      </div>

      <div class="signoff-actions">
        <UaBtn variant="outlined" @click="emit('close')">Cancel</UaBtn>
        <UaBtn color="primary" variant="flat" :disabled="eligibleEntries.length === 0" @click="confirm">
          Confirm Sign Off
        </UaBtn>
      </div>
    </div>
  </UaModal>
</template>

<style scoped>
.signoff-modal {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-md);
}

.signoff-description {
  margin: 0;
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
}

.targets-list {
  display: flex;
  flex-direction: column;
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  overflow: hidden;
  max-height: 320px;
  overflow-y: auto;
}

.target-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--ua-spacing-sm) var(--ua-spacing-md);
  border-bottom: 1px solid var(--ua-border-color);
}

.target-row:last-child {
  border-bottom: none;
}

.target-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.target-name {
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
  color: var(--ua-text-primary);
}

.target-date {
  font-size: var(--ua-font-size-xs);
  color: var(--ua-text-secondary);
}

.target-area {
  font-size: var(--ua-font-size-xs);
  color: var(--ua-text-secondary);
}

.no-eligible {
  padding: var(--ua-spacing-md);
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  text-align: center;
}

.signoff-actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--ua-spacing-sm);
  padding-top: var(--ua-spacing-sm);
  border-top: 1px solid var(--ua-border-color);
}
</style>
