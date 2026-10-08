<script setup lang="ts">
import { Permissions } from '@/api-access/generated/models';
import type {
  SchedulingTimeOffEntryResponse as TimeOffEntryResponse,
  SchedulingTimeOffSeriesResponse as TimeOffSeriesResponse,
  UserResponse,
} from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import { getTimeOffSeries, useUserTimeOffEntries } from '@/modules/timeoff/api/timeOffApi';
import { useLeaveTypeOptions } from '@/modules/timeoff/composables/useLeaveTypeOptions';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import { mdiChevronDown, mdiChevronUp, mdiDelete, mdiPencil, mdiPlus, mdiRepeat } from '@mdi/js';
import { DateTime } from 'luxon';
import { RRule } from 'rrule';
import { computed, ref } from 'vue';
import { useUsersStore } from '@/stores/Users';
import DeleteUserTimeOffModal from '../components/DeleteUserTimeOffModal.vue';
import UserTimeOffModal from '../components/UserTimeOffModal.vue';

const props = defineProps<{
  user: UserResponse;
}>();

const accessControl = useAccessControl();
const canViewLeave = computed(() => accessControl.hasPermission(Permissions.UsersTimeOffView));
const canCreateLeave = computed(() => accessControl.hasPermission(Permissions.UsersTimeOffCreate));
const canEditLeave = computed(() => accessControl.hasPermission(Permissions.UsersTimeOffEdit));
const canDeleteLeave = computed(() => accessControl.hasPermission(Permissions.UsersTimeOffDelete));
const usersStore = useUsersStore();

const { data: entries, error, isFetching, execute: fetchEntries } = useUserTimeOffEntries(props.user.id);
const userSeries = ref<TimeOffSeriesResponse[]>([]);
const seriesError = ref<Error | null>(null);
const isFetchingSeries = ref(false);
const { leaveTypeOptions } = useLeaveTypeOptions();

const openScope = ref<'event' | 'series'>('event');
const scopeChoice = ref<{ entry: TimeOffEntryResponse; action: 'edit' | 'delete' } | null>(null);
const showLeaveModal = ref(false);
const selectedEntry = ref<TimeOffEntryResponse | null>(null);
const showDeleteModal = ref(false);
const selectedDeleteEntry = ref<TimeOffEntryResponse | null>(null);

if (canViewLeave.value) {
  void loadAll();
}

const chooseScope = (scope: 'event' | 'series') => {
  const choice = scopeChoice.value;
  if (!choice) return;
  scopeChoice.value = null;
  if (choice.action === 'edit') {
    setOpenEntry(choice.entry, scope);
    showLeaveModal.value = true;
  } else {
    setOpenEntry(choice.entry, scope);
    selectedDeleteEntry.value = choice.entry;
    showDeleteModal.value = true;
  }
};

const seriesHasOtherUsers = (entry: TimeOffEntryResponse) =>
  (entry.seriesUserIds ?? entry.assignedUserIds ?? []).some((userId) => userId !== props.user.id);

const headers = [
  { title: '', key: 'data-table-expand', sortable: false, width: 56 },
  { title: 'Leave Type', key: 'leaveTypeName', sortable: true },
  { title: 'Date', key: 'date', sortable: true },
  { title: 'Time', key: 'time', sortable: false },
  { title: 'Repeat', key: 'recurrence', sortable: false },
  { title: 'Status', key: 'statusTypeCode', sortable: true },
  { title: 'Notes', key: 'notes', sortable: false },
  { title: 'Actions', key: 'actions', sortable: false, align: 'end' as const, width: 120 },
];

const leaveTypeNames = computed(
  () => new Map(leaveTypeOptions.value.map((option) => [Number(option.code), option.description])),
);

const toLocal = (value: string | null | undefined, zone: string | null | undefined) => {
  if (!value) {
    return null;
  }
  const dateTime = DateTime.fromISO(value, { zone: zone || 'America/Vancouver' });
  return dateTime.isValid ? dateTime : null;
};

const rows = computed(() =>
  (entries.value ?? []).map((entry) => {
    const start = toLocal(entry.startAtUtc, entry.timeZoneId);
    const end = toLocal(entry.endAtUtc, entry.timeZoneId);
    return {
      ...entry,
      rowId: `entry-${entry.id}`,
      rowType: 'entry' as const,
      leaveTypeName: leaveTypeNames.value.get(entry.leaveTypeId ?? -1) ?? `Leave type ${entry.leaveTypeId}`,
      date: start?.toFormat('yyyy-MM-dd') ?? '-',
      recurrence: entry.seriesRecurrenceRule ?? '',
      time: start && end ? `${start.toFormat('h:mm a')} - ${end.toFormat('h:mm a')}` : '-',
    };
  }),
);
const tableRows = computed(() => [
  ...rows.value.filter((entry) => !entry.timeOffSeriesId),
  ...(userSeries.value ?? []).map((series) => {
    const start = toLocal(series.startAtUtc, series.timeZoneId);
    const end = toLocal(series.endAtUtc, series.timeZoneId);
    return {
      ...series,
      rowId: `series-${series.id}`,
      rowType: 'series' as const,
      id: series.id,
      timeOffSeriesId: series.id,
      statusTypeCode: series.statusTypeCode,
      leaveTypeName: leaveTypeNames.value.get(series.leaveTypeId ?? -1) ?? `Leave type ${series.leaveTypeId}`,
      date: start?.toFormat('yyyy-MM-dd') ?? '-',
      time: start && end ? `${start.toFormat('h:mm a')} - ${end.toFormat('h:mm a')}` : '-',
      notes: series.notes,
      recurrence: series.recurrenceRule ?? '',
      assignedUserIds: series.userIds,
      seriesStartAtUtc: series.startAtUtc,
      seriesEndAtUtc: series.endAtUtc,
      seriesTimeZoneId: series.timeZoneId,
      seriesRecurrenceRule: series.recurrenceRule,
      seriesUserIds: series.userIds,
    };
  }),
]);

const isDraft = (entry: TimeOffEntryResponse) => entry.statusTypeCode?.toLowerCase() === 'draft';

const expanded = ref<string[]>([]);

const describeRecurrence = (rule: string | null | undefined) => {
  if (!rule) {
    return 'Does not repeat';
  }
  try {
    const text = RRule.fromString(rule.startsWith('RRULE:') ? rule : `RRULE:${rule}`).toText();
    return text.charAt(0).toUpperCase() + text.slice(1);
  } catch {
    return rule;
  }
};

const statusColor = (code: string | null | undefined) => {
  switch (code?.toLowerCase()) {
    case 'draft':
      return 'warning';
    case 'cancelled':
      return 'error';
    case 'approved':
    case 'confirmed':
      return 'success';
    default:
      return undefined;
  }
};

const employeeNames = (userIds: string[] | undefined) =>
  (userIds ?? [])
    .map((id) => {
      const user = usersStore.getUserById(id);
      return user ? `${user.firstName} ${user.lastName}`.trim() : null;
    })
    .filter((name): name is string => name !== null)
    .join(', ') || '-';

const setOpenEntry = (entry: TimeOffEntryResponse, scope: 'event' | 'series') => {
  openScope.value = scope;
  selectedEntry.value = entry;
};

const handleOpenAddModal = () => {
  selectedEntry.value = null;
  openScope.value = 'event';
  showLeaveModal.value = true;
};

const handleOpenEditModal = (entry: TimeOffEntryResponse & { rowType?: string }) => {
  if (entry.rowType === 'series' && seriesHasOtherUsers(entry)) {
    scopeChoice.value = { entry, action: 'edit' };
    return;
  }
  if (entry.timeOffSeriesId != null && entry.rowType !== 'series') {
    chooseScopeForUser(entry, 'edit');
  } else {
    setOpenEntry(entry, entry.rowType === 'series' ? 'series' : 'event');
    showLeaveModal.value = true;
  }
};

const chooseScopeForUser = (entry: TimeOffEntryResponse, action: 'edit' | 'delete') => {
  scopeChoice.value = { entry, action };
};

const handleCloseLeaveModal = () => {
  showLeaveModal.value = false;
  selectedEntry.value = null;
};

const handleSaved = async () => {
  handleCloseLeaveModal();
  await loadAll();
};

// Series ids come from the entries, so entries must finish loading first.
async function loadAll() {
  const userLoadResult = await Promise.allSettled([fetchEntries(), usersStore.ensureAllUsers()]);
  const employeeLoadError =
    userLoadResult[1].status === 'rejected'
      ? userLoadResult[1].reason instanceof Error
        ? userLoadResult[1].reason
        : new Error('Failed to load employee names.')
      : null;
  await loadSeriesRows();
  if (employeeLoadError) {
    seriesError.value = employeeLoadError;
  }
}

async function loadSeriesRows() {
  isFetchingSeries.value = true;
  seriesError.value = null;
  try {
    const ids = new Set(
      (entries.value ?? []).map((entry) => entry.timeOffSeriesId).filter((id): id is number => id != null),
    );
    const responses = await Promise.all([...ids].map((id) => getTimeOffSeries(id)));
    const firstError = responses.find((response) => response.error.value)?.error.value;
    if (firstError) {
      seriesError.value = new Error(firstError.message || 'Failed to load time-off series.');
      return;
    }
    userSeries.value = responses
      .map((response) => response.data.value)
      .filter((series): series is TimeOffSeriesResponse => series !== null && series !== undefined);
  } finally {
    isFetchingSeries.value = false;
  }
}

const handleOpenDeleteModal = (entry: TimeOffEntryResponse & { rowType?: string }) => {
  if (entry.rowType === 'series' && seriesHasOtherUsers(entry)) {
    setOpenEntry(entry, 'series');
    selectedDeleteEntry.value = entry;
    showDeleteModal.value = true;
    return;
  }
  if (entry.timeOffSeriesId != null && entry.rowType !== 'series') {
    chooseScopeForUser(entry, 'delete');
  } else {
    setOpenEntry(entry, entry.rowType === 'series' ? 'series' : 'event');
    selectedDeleteEntry.value = entry;
    showDeleteModal.value = true;
  }
};

const handleCloseDeleteModal = () => {
  showDeleteModal.value = false;
  selectedDeleteEntry.value = null;
};

const deleteLeaveTypeName = computed(
  () => leaveTypeNames.value.get(selectedDeleteEntry.value?.leaveTypeId ?? -1) ?? 'this time off',
);
const deleteDateLabel = computed(
  () =>
    toLocal(selectedDeleteEntry.value?.startAtUtc, selectedDeleteEntry.value?.timeZoneId)?.toFormat('yyyy-MM-dd') ?? '',
);
const deleteOtherEmployeeNames = computed(() => {
  const entry = selectedDeleteEntry.value;
  if (!entry) {
    return '';
  }

  const otherUserIds = (entry.seriesUserIds ?? entry.assignedUserIds ?? []).filter(
    (userId) => userId !== props.user.id,
  );
  return otherUserIds.length > 0 ? employeeNames(otherUserIds) : '';
});
</script>

<template>
  <div v-if="!canViewLeave" class="user-timeoff-view">
    <UaPlaceholderPage title="Leave/Time off" description="You do not have permission to view leave records." />
  </div>
  <div v-else class="user-timeoff-view">
    <div class="user-timeoff-view__header">
      <h3>Leave/Time off</h3>
      <UaBtn v-if="canCreateLeave" :prepend-icon="mdiPlus" @click="handleOpenAddModal">Add Leave</UaBtn>
    </div>

    <UaAlert v-if="error || seriesError" type="error" :closable="false">
      Failed to load leave records: {{ error?.message || seriesError?.message }}
    </UaAlert>

    <div v-if="isFetching || isFetchingSeries" class="user-timeoff-view__loading">Loading leave records...</div>

    <UaDataTable
      v-else-if="tableRows.length"
      :headers="headers"
      :items="tableRows"
      :items-per-page="-1"
      item-value="rowId"
      show-expand
      v-model:expanded="expanded"
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

      <template #[`item.leaveTypeName`]="{ item }">
        <span class="user-timeoff-view__type">
          {{ item.leaveTypeName }}
          <v-icon v-if="item.rowType === 'series'" :icon="mdiRepeat" size="small" title="Repeating series" />
        </span>
      </template>

      <template #[`item.statusTypeCode`]="{ item }">
        <v-chip v-if="item.statusTypeCode" size="small" label :color="statusColor(item.statusTypeCode)">
          {{ item.statusTypeCode }}
        </v-chip>
        <span v-else>-</span>
      </template>

      <template #[`item.notes`]="{ item }">
        {{ item.notes?.trim() || '-' }}
      </template>

      <template #[`item.recurrence`]="{ item }">
        {{ item.rowType === 'series' ? describeRecurrence(item.recurrence) : item.recurrence ? 'Repeats' : '-' }}
      </template>

      <template #expanded-row="{ columns, item }">
        <tr v-if="item.rowType === 'series'" class="user-timeoff-view__expanded">
          <td :colspan="columns.length">
            <div class="user-timeoff-view__details">
              <dl class="user-timeoff-view__summary">
                <div>
                  <dt>Repeats</dt>
                  <dd>{{ describeRecurrence(item.recurrence) }}</dd>
                </div>
                <div>
                  <dt>First occurrence</dt>
                  <dd>{{ item.date }}, {{ item.time }}</dd>
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

      <template #[`item.actions`]="{ item }">
        <div class="user-timeoff-view__actions">
          <UaBtn
            v-if="item.rowType === 'series' && canEditLeave && isDraft(item)"
            icon
            variant="text"
            size="small"
            aria-label="Edit leave series"
            title="Edit leave series"
            @click="handleOpenEditModal(item)"
          >
            <v-icon :icon="mdiPencil" />
          </UaBtn>
          <UaBtn
            v-if="item.rowType === 'entry' && !item.timeOffSeriesId && canEditLeave && isDraft(item)"
            icon
            variant="text"
            size="small"
            aria-label="Edit leave record"
            title="Edit leave record"
            @click="handleOpenEditModal(item)"
          >
            <v-icon :icon="mdiPencil" />
          </UaBtn>
          <UaBtn
            v-if="canDeleteLeave && isDraft(item)"
            icon
            variant="text"
            size="small"
            color="error"
            aria-label="Delete leave record"
            title="Delete leave record"
            @click="handleOpenDeleteModal(item)"
          >
            <v-icon :icon="mdiDelete" />
          </UaBtn>
        </div>
      </template>
    </UaDataTable>

    <UaPlaceholderPage
      v-else-if="!error"
      title="No leave records"
      description="No leave records have been recorded for this user yet."
    />

    <UaModal
      v-if="scopeChoice"
      :title="
        seriesHasOtherUsers(scopeChoice.entry)
          ? scopeChoice.action === 'edit'
            ? 'Edit Time Off Series'
            : 'Delete Time Off Series'
          : 'Open Time Off'
      "
      :tone="scopeChoice.action === 'delete' && seriesHasOtherUsers(scopeChoice.entry) ? 'error' : 'default'"
      @close="scopeChoice = null"
    >
      <template v-if="scopeChoice.action === 'edit' && seriesHasOtherUsers(scopeChoice.entry)">
        <p>
          Editing this time-off series will change time-off for
          <strong>
            {{
              employeeNames(
                (scopeChoice.entry.seriesUserIds ?? scopeChoice.entry.assignedUserIds)?.filter(
                  (userId) => userId !== user.id,
                ),
              )
            }}
          </strong>
          in addition to you. Do you want to continue?
        </p>
      </template>
      <template v-else-if="scopeChoice.action === 'delete' && seriesHasOtherUsers(scopeChoice.entry)">
        <p>
          Deleting this time-off series will remove time-off for
          <strong>
            {{
              employeeNames(
                (scopeChoice.entry.seriesUserIds ?? scopeChoice.entry.assignedUserIds)?.filter(
                  (userId) => userId !== user.id,
                ),
              )
            }}
          </strong>
          in addition to you. This permanently removes all draft occurrences and cannot be undone. Do you want to
          continue?
        </p>
      </template>
      <template v-else>
        <p>This is one occurrence in a time-off series. What do you want to open?</p>
        <div class="user-timeoff-view__scope-actions">
          <UaBtn variant="outlined" @click="chooseScope('event')">Only this occurrence</UaBtn>
          <UaBtn color="primary" variant="flat" @click="chooseScope('series')">The entire series</UaBtn>
        </div>
      </template>

      <template #actions v-if="seriesHasOtherUsers(scopeChoice.entry)">
        <UaBtn variant="outlined" @click="scopeChoice = null">Cancel</UaBtn>
        <UaBtn :color="scopeChoice.action === 'delete' ? 'error' : 'primary'" @click="chooseScope('series')">
          {{ scopeChoice.action === 'delete' ? 'Delete' : 'Edit series' }}
        </UaBtn>
      </template>
    </UaModal>

    <UserTimeOffModal
      v-if="showLeaveModal"
      :user="user"
      :entry="selectedEntry"
      :scope="openScope"
      @close="handleCloseLeaveModal"
      @save="handleSaved"
    />

    <DeleteUserTimeOffModal
      v-if="showDeleteModal && selectedDeleteEntry"
      :entry="selectedDeleteEntry"
      :scope="openScope"
      :leave-type-name="deleteLeaveTypeName"
      :date-label="deleteDateLabel"
      :other-employee-names="deleteOtherEmployeeNames"
      @close="handleCloseDeleteModal"
      @deleted="loadAll"
    />
  </div>
</template>

<style scoped>
.user-timeoff-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.user-timeoff-view__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.user-timeoff-view__loading {
  color: var(--ua-text-secondary);
}

.user-timeoff-view__scope-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.user-timeoff-view__actions {
  display: flex;
  gap: 4px;
  justify-content: flex-end;
}

.user-timeoff-view__type {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.user-timeoff-view__expanded > td {
  background-color: rgba(var(--v-theme-surface-variant), 0.2);
  padding: 0 !important;
}

.user-timeoff-view__details {
  padding: 12px 24px 16px 56px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.user-timeoff-view__summary {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 12px 24px;
  margin: 0;
}

.user-timeoff-view__summary dt {
  font-size: 0.75rem;
  color: var(--ua-text-secondary);
}

.user-timeoff-view__summary dd {
  margin: 0;
}
</style>
