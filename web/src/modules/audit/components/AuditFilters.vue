<script setup lang="ts">
import UaAutocomplete from '@/shared/components/UaAutocomplete.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import { mdiFilterRemove, mdiMagnify } from '@mdi/js';
import type { SelectOption } from '@/types/select';
import { AUDIT_ACTION_OPTIONS } from '../constants';

defineProps<{
  entityTypeOptions: SelectOption[];
  changedFieldOptions: SelectOption[];
  actorOptions: SelectOption[];
  isLoadingEntityTypes?: boolean;
  isLoadingFields?: boolean;
  isLoadingActors?: boolean;
  loading?: boolean;
  canApply: boolean;
}>();

const emit = defineEmits<{
  apply: [];
  clear: [];
  'search:actor': [value: string];
}>();

const entityType = defineModel<string | null>('entityType', { default: null });
const entityPk = defineModel<string | null>('entityPk', { default: null });
const changedFields = defineModel<string[]>('changedFields', { default: () => [] });
const actorUserId = defineModel<string | null>('actorUserId', { default: null });
const action = defineModel<string | null>('action', { default: null });
const fromDate = defineModel<string | null>('fromDate', { default: null });
const toDate = defineModel<string | null>('toDate', { default: null });
</script>

<template>
  <div class="filters-panel">
    <h3 class="filters-title">Filters</h3>

    <div class="filter-group">
      <label class="filter-label" for="audit-filter-entity-type">
        Entity Type
        <span class="filter-required">*</span>
      </label>
      <UaSelect
        id="audit-filter-entity-type"
        v-model="entityType"
        :items="entityTypeOptions"
        :loading="isLoadingEntityTypes"
        placeholder="Select an entity type"
        density="compact"
      />
    </div>

    <div class="filter-group">
      <label class="filter-label" for="audit-filter-entity-id">Entity ID</label>
      <UaTextField
        id="audit-filter-entity-id"
        v-model="entityPk"
        label=""
        placeholder="Search by entity ID"
        density="compact"
        clearable
      />
    </div>

    <div class="filter-group">
      <label class="filter-label" for="audit-filter-changed-field">Changed Field</label>
      <UaSelect
        id="audit-filter-changed-field"
        v-model="changedFields"
        :items="changedFieldOptions"
        :loading="isLoadingFields"
        :disabled="!entityType"
        placeholder="Select fields"
        density="compact"
        multiple
        chips
        closable-chips
        clearable
      />
    </div>

    <div class="filter-group">
      <label class="filter-label" for="audit-filter-actor">Actor</label>
      <UaAutocomplete
        id="audit-filter-actor"
        v-model="actorUserId"
        :items="actorOptions"
        :loading="isLoadingActors"
        placeholder="Search by actor name"
        density="compact"
        clearable
        @update:search="emit('search:actor', $event)"
      />
    </div>

    <div class="filter-group">
      <label class="filter-label" for="audit-filter-action">Action</label>
      <UaSelect id="audit-filter-action" v-model="action" :items="AUDIT_ACTION_OPTIONS" density="compact" />
    </div>

    <div class="filter-group">
      <label class="filter-label" for="audit-filter-from-date">
        From
        <span class="filter-required">*</span>
      </label>
      <UaTextField id="audit-filter-from-date" v-model="fromDate" label="" type="date" density="compact" />
    </div>

    <div class="filter-group">
      <label class="filter-label" for="audit-filter-to-date">
        To
        <span class="filter-required">*</span>
      </label>
      <UaTextField id="audit-filter-to-date" v-model="toDate" label="" type="date" density="compact" />
    </div>

    <div class="filter-actions">
      <UaBtn variant="text" :prepend-icon="mdiFilterRemove" @click="emit('clear')">Clear</UaBtn>
      <UaBtn
        color="primary"
        variant="flat"
        :prepend-icon="mdiMagnify"
        :loading="loading"
        :disabled="!canApply"
        @click="emit('apply')"
      >
        Search
      </UaBtn>
    </div>
  </div>
</template>

<style scoped>
.filters-panel {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-md);
  padding: var(--ua-spacing-lg);
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  background: rgb(var(--v-theme-surface));
}

.filters-title {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
  margin: 0;
}

.filter-group {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xs);
}

.filter-label {
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
  color: var(--ua-text-primary);
}

.filter-required {
  color: rgb(var(--v-theme-error));
  margin-left: 2px;
}

.filter-actions {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-sm);
  padding-top: var(--ua-spacing-sm);
  border-top: 1px solid var(--ua-border-color);
}
</style>
