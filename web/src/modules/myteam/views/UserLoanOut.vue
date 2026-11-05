<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { DateTime } from 'luxon';
import { RRule } from 'rrule';
import { mdiChevronDown, mdiChevronUp, mdiDelete, mdiPencil, mdiPlus, mdiRepeat } from '@mdi/js';
import {
  Permissions,
  type LoanOutEntryResponse,
  type LoanOutSeriesResponse,
  type UserResponse,
} from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import { useUserLoanOutEntries, useUserLoanOutSeries } from '@/modules/loanout/api/loanOutApi';
import { useLocationsStore } from '@/stores/LocationsStore';
import { useUsersStore } from '@/stores/Users';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import DeleteUserLoanOutModal from '../components/DeleteUserLoanOutModal.vue';
import UserLoanOutModal from '../components/UserLoanOutModal.vue';

const props = defineProps<{ user: UserResponse }>();
type LoanOutRow = LoanOutEntryResponse & { series?: LoanOutSeriesResponse };
type Scope = 'event' | 'series';
const accessControl = useAccessControl();
const locationsStore = useLocationsStore();
const usersStore = useUsersStore();
const canView = computed(
  () =>
    Boolean(accessControl.featureFlags.value?.LoanOut?.enabled) &&
    accessControl.hasPermission(Permissions.UsersLoanOutView),
);
const canCreate = computed(() => accessControl.hasPermission(Permissions.UsersLoanOutCreate));
const canEdit = computed(() => accessControl.hasPermission(Permissions.UsersLoanOutEdit));
const canDelete = computed(() => accessControl.hasPermission(Permissions.UsersLoanOutDelete));
const entries = ref<LoanOutEntryResponse[]>([]);
const series = ref<LoanOutSeriesResponse[]>([]);
const isLoading = ref(false);
const apiError = ref('');
const lookupError = ref('');
const selectedEntry = ref<LoanOutRow | null>(null);
const openScope = ref<Scope>('event');
const showModal = ref(false);
const showDeleteModal = ref(false);
const confirmation = ref<{ entry: LoanOutRow; scope: Scope; action: 'edit' } | null>(null);
const scopeChoice = ref<{ entry: LoanOutRow; action: 'edit' | 'delete' } | null>(null);
const expanded = ref<string[]>([]);
let controller: AbortController | undefined;
let loadVersion = 0;

const headers = [
  { title: '', key: 'data-table-expand', sortable: false, width: 56 },
  { title: 'Loan-out Location', key: 'loanOutLocationName', sortable: true },
  { title: 'Date', key: 'date', sortable: true },
  { title: 'Repeat', key: 'recurrence', sortable: false },
  { title: 'Notes', key: 'notes', sortable: false },
  { title: 'Actions', key: 'actions', sortable: false, align: 'end' as const, width: 120 },
];
function locationName(id?: number) {
  return id == null ? '-' : locationsStore.entitiesMap[id]?.name || `Location ${id}`;
}
function toLocal(value?: string | null, zone?: string | null) {
  if (!value) return null;
  const date = DateTime.fromISO(value, { zone: zone || 'America/Vancouver' });
  return date.isValid ? date : null;
}
function displayFields(entry: LoanOutEntryResponse | LoanOutSeriesResponse) {
  const start = toLocal(entry.startAtUtc, entry.timeZoneId);
  return {
    loanOutLocationName: locationName(entry.loanOutLocationId),
    date: start?.toFormat('yyyy-MM-dd') ?? '-',
  };
}
const entryRows = computed(() =>
  entries.value.map((entry) => ({
    ...entry,
    ...displayFields(entry),
    series: series.value.find((item) => item.id === entry.loanOutSeriesId),
    rowId: `entry-${entry.id}`,
    rowType: 'entry' as const,
    recurrence: '',
  })),
);
const tableRows = computed(() => [
  ...entryRows.value.filter((entry) => !entry.loanOutSeriesId || !entry.series),
  ...series.value.map((item) => ({
    ...item,
    ...displayFields(item),
    loanOutSeriesId: item.id,
    series: item,
    rowId: `series-${item.id}`,
    rowType: 'series' as const,
    recurrence: item.recurrenceRule ?? '',
  })),
]);
const isDraft = (entry: LoanOutEntryResponse) => entry.statusTypeCode?.toLowerCase() === 'draft';
function describeRecurrence(rule?: string | null) {
  if (!rule) return 'Does not repeat';
  try {
    const text = RRule.fromString(rule.startsWith('RRULE:') ? rule : `RRULE:${rule}`).toText();
    return text.charAt(0).toUpperCase() + text.slice(1);
  } catch {
    return rule;
  }
}
function employeeNames(ids?: string[]) {
  return (
    (ids ?? [])
      .map((id) => {
        const user = usersStore.getUserById(id);
        return user ? `${user.firstName} ${user.lastName}`.trim() : id;
      })
      .join(', ') || '-'
  );
}
function otherEmployeeNames(entry: LoanOutRow, scope: Scope) {
  return employeeNames(
    (scope === 'series' ? (entry.series?.userIds ?? entry.userIds) : entry.userIds)?.filter(
      (id) => id !== props.user.id,
    ),
  );
}
function hasOtherEmployees(entry: LoanOutRow, scope: Scope) {
  return (
    (scope === 'series' ? (entry.series?.userIds ?? entry.userIds) : entry.userIds)?.some(
      (id) => id !== props.user.id,
    ) ?? false
  );
}
function open(entry: LoanOutRow, scope: Scope, action: 'edit' | 'delete') {
  if (!canView.value || !(action === 'edit' ? canEdit.value : canDelete.value)) return;
  if (action === 'edit' && hasOtherEmployees(entry, scope)) {
    confirmation.value = { entry, scope, action };
    return;
  }
  selectedEntry.value = entry;
  openScope.value = scope;
  if (action === 'edit') showModal.value = true;
  else showDeleteModal.value = true;
}
function requestOpen(entry: LoanOutRow & { rowType?: string }, action: 'edit' | 'delete') {
  if (entry.loanOutSeriesId != null && entry.rowType !== 'series') {
    scopeChoice.value = { entry, action };
    return;
  }
  open(entry, entry.rowType === 'series' ? 'series' : 'event', action);
}
function chooseScope(scope: Scope) {
  const choice = scopeChoice.value;
  scopeChoice.value = null;
  if (choice) open(choice.entry, scope, choice.action);
}
function confirmEdit() {
  const choice = confirmation.value;
  confirmation.value = null;
  if (!choice || !canView.value || !canEdit.value) return;
  selectedEntry.value = choice.entry;
  openScope.value = choice.scope;
  showModal.value = true;
}
function openAdd() {
  if (!canView.value || !canCreate.value) return;
  selectedEntry.value = null;
  openScope.value = 'event';
  showModal.value = true;
}
async function saved() {
  showModal.value = false;
  selectedEntry.value = null;
  await loadAll();
}
async function loadAll() {
  controller?.abort();
  const version = ++loadVersion;
  if (!canView.value) return;
  controller = new AbortController();
  const entryRequest = useUserLoanOutEntries(props.user.id, controller.signal);
  const seriesRequest = useUserLoanOutSeries(props.user.id, controller.signal);
  isLoading.value = true;
  apiError.value = '';
  lookupError.value = '';
  try {
    const results = await Promise.allSettled([
      entryRequest.execute(),
      seriesRequest.execute(),
      usersStore.ensureAllUsers(),
      locationsStore.selectOptions.length ? Promise.resolve() : locationsStore.getEntities(),
    ]);
    if (version !== loadVersion) return;
    entries.value = entryRequest.error.value ? [] : (entryRequest.data.value ?? []);
    series.value = seriesRequest.error.value ? [] : (seriesRequest.data.value ?? []);
    const readErrors = [
      entryRequest.error.value?.message,
      seriesRequest.error.value?.message,
      ...results
        .slice(0, 2)
        .flatMap((result) =>
          result.status === 'rejected'
            ? [result.reason instanceof Error ? result.reason.message : 'Failed to load loan-out records.']
            : [],
        ),
    ].filter(Boolean);
    apiError.value = readErrors.join(' ');
    if (results.slice(2).some((result) => result.status === 'rejected')) {
      lookupError.value = 'Some employee or location names could not be loaded.';
    }
  } finally {
    if (version === loadVersion) isLoading.value = false;
  }
}
watch(
  [() => props.user.id, canView],
  () => {
    entries.value = [];
    series.value = [];
    showModal.value = false;
    showDeleteModal.value = false;
    confirmation.value = null;
    scopeChoice.value = null;
    void loadAll();
  },
  { immediate: true },
);
onBeforeUnmount(() => {
  ++loadVersion;
  controller?.abort();
});
</script>

<template>
  <UaPlaceholderPage
    v-if="!canView"
    title="Loan-out"
    description="Loan-out is unavailable or you do not have permission to view these records."
  />
  <div v-else class="user-loan-out">
    <div class="user-loan-out__header">
      <h3>Loan-out</h3>
      <UaBtn v-if="canCreate" :prepend-icon="mdiPlus" @click="openAdd">Add Loan-out</UaBtn>
    </div>
    <UaAlert v-if="apiError" type="error" :closable="false">Failed to load loan-out records: {{ apiError }}</UaAlert>
    <UaAlert v-if="lookupError" type="warning" :closable="false">{{ lookupError }}</UaAlert>
    <div v-if="isLoading" class="user-loan-out__loading">Loading loan-out records...</div>
    <UaDataTable
      v-else-if="tableRows.length"
      v-model:expanded="expanded"
      :headers="headers"
      :items="tableRows"
      :items-per-page="-1"
      item-value="rowId"
      show-expand
      density="comfortable"
      hide-default-footer
    >
      <template #[`item.data-table-expand`]="{ item, isExpanded, toggleExpand, internalItem }">
        <UaBtn
          v-if="item.rowType === 'series'"
          icon
          variant="text"
          size="small"
          :aria-label="isExpanded(internalItem) ? 'Hide series details' : 'Show series details'"
          :title="isExpanded(internalItem) ? 'Hide series details' : 'Show series details'"
          @click="toggleExpand(internalItem)"
        >
          <v-icon :icon="isExpanded(internalItem) ? mdiChevronUp : mdiChevronDown" />
        </UaBtn>
      </template>
      <template #[`item.loanOutLocationName`]="{ item }">
        <span class="user-loan-out__type">
          {{ item.loanOutLocationName }}
          <v-icon v-if="item.rowType === 'series'" :icon="mdiRepeat" size="small" title="Repeating series" />
        </span>
      </template>
      <template #[`item.notes`]="{ item }">{{ item.notes?.trim() || '-' }}</template>
      <template #[`item.recurrence`]="{ item }">{{
        item.rowType === 'series' ? describeRecurrence(item.recurrence) : '-'
      }}</template>
      <template #[`item.actions`]="{ item }">
        <div class="user-loan-out__actions">
          <UaBtn
            v-if="canEdit && isDraft(item)"
            icon
            variant="text"
            size="small"
            :aria-label="item.rowType === 'series' ? 'Edit loan-out series' : 'Edit loan-out record'"
            :title="item.rowType === 'series' ? 'Edit loan-out series' : 'Edit loan-out record'"
            @click="requestOpen(item, 'edit')"
            ><v-icon :icon="mdiPencil"
          /></UaBtn>
          <UaBtn
            v-if="canDelete && isDraft(item)"
            icon
            variant="text"
            size="small"
            color="error"
            :aria-label="item.rowType === 'series' ? 'Delete loan-out series' : 'Delete loan-out record'"
            :title="item.rowType === 'series' ? 'Delete loan-out series' : 'Delete loan-out record'"
            @click="requestOpen(item, 'delete')"
            ><v-icon :icon="mdiDelete"
          /></UaBtn>
        </div>
      </template>
      <template #expanded-row="{ columns, item }">
        <tr v-if="item.rowType === 'series'" class="user-loan-out__expanded">
          <td :colspan="columns.length">
            <div class="user-loan-out__details">
              <dl class="user-loan-out__summary">
                <div>
                  <dt>Repeats</dt>
                  <dd>{{ describeRecurrence(item.recurrence) }}</dd>
                </div>
                <div>
                  <dt>First occurrence</dt>
                  <dd>{{ item.date }}</dd>
                </div>
                <div>
                  <dt>Time zone</dt>
                  <dd>{{ item.timeZoneId || '-' }}</dd>
                </div>
                <div>
                  <dt>Employees</dt>
                  <dd>{{ employeeNames(item.userIds) }}</dd>
                </div>
                <div>
                  <dt>Notes</dt>
                  <dd>{{ item.notes?.trim() || '-' }}</dd>
                </div>
              </dl>
            </div>
          </td>
        </tr>
      </template>
    </UaDataTable>
    <UaPlaceholderPage
      v-else-if="!apiError"
      title="No loan-out records"
      description="No loan-out records have been recorded for this user yet."
    />

    <UaModal v-if="scopeChoice" title="Open Loan-out" @close="scopeChoice = null">
      <p>This is one occurrence in a loan-out series. What do you want to open?</p>
      <div class="user-loan-out__scope-actions">
        <UaBtn variant="outlined" @click="chooseScope('event')">Only this occurrence</UaBtn>
        <UaBtn color="primary" variant="flat" @click="chooseScope('series')">The entire series</UaBtn>
      </div>
    </UaModal>
    <UaModal
      v-if="confirmation"
      :title="confirmation.scope === 'series' ? 'Edit Loan-out Series' : 'Edit Loan-out'"
      @close="confirmation = null"
    >
      <p>
        Editing this {{ confirmation.scope === 'series' ? 'series' : 'occurrence' }} will also change loan-out for
        <strong>{{ otherEmployeeNames(confirmation.entry, confirmation.scope) }}</strong
        >. Do you want to continue?
      </p>
      <template #actions>
        <UaBtn variant="outlined" @click="confirmation = null">Cancel</UaBtn>
        <UaBtn color="primary" variant="flat" @click="confirmEdit">
          {{ confirmation.scope === 'series' ? 'Edit series' : 'Edit occurrence' }}
        </UaBtn>
      </template>
    </UaModal>
    <UserLoanOutModal
      v-if="showModal"
      :user="user"
      :entry="selectedEntry"
      :scope="openScope"
      @close="showModal = false"
      @save="saved"
    />
    <DeleteUserLoanOutModal
      v-if="showDeleteModal && selectedEntry"
      :entry="selectedEntry"
      :scope="openScope"
      :loan-out-location-name="locationName(selectedEntry.loanOutLocationId)"
      :date-label="displayFields(selectedEntry).date"
      :other-employee-names="
        hasOtherEmployees(selectedEntry, openScope) ? otherEmployeeNames(selectedEntry, openScope) : ''
      "
      @close="showDeleteModal = false"
      @deleted="loadAll"
    />
  </div>
</template>

<style scoped>
.user-loan-out {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.user-loan-out__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.user-loan-out__actions {
  display: flex;
  justify-content: flex-end;
  gap: 4px;
}
.user-loan-out__scope-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.user-loan-out__loading {
  color: var(--ua-text-secondary);
}
.user-loan-out__type {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.user-loan-out__expanded > td {
  background-color: rgba(var(--v-theme-surface-variant), 0.2);
  padding: 0 !important;
}
.user-loan-out__details {
  padding: 12px 24px 16px 56px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.user-loan-out__summary {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 12px 24px;
  margin: 0;
}
.user-loan-out__summary dt {
  font-size: 0.75rem;
  color: var(--ua-text-secondary);
}
.user-loan-out__summary dd {
  margin: 0;
}
</style>
