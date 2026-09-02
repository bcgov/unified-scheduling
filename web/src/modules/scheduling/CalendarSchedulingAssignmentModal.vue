<script setup lang="ts">
import { mdiDelete } from '@mdi/js';
import RRuleEditor from '@/components/recurrence/RRuleEditor.vue';
import { getApiSchedulingShiftsEntries, getApiSchedulingShiftsSeries } from '@/api-access/generated/shift/shift';
import type { AssignmentEntryResponse } from '@/api-access/generated/models/assignmentEntryResponse';
import type { AssignmentSeriesResponse } from '@/api-access/generated/models/assignmentSeriesResponse';
import type { ShiftEntryResponse } from '@/api-access/generated/models/shiftEntryResponse';
import type { ShiftSeriesResponse } from '@/api-access/generated/models/shiftSeriesResponse';
import type { UserResponse } from '@/api-access/generated/models/userResponse';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import UaTextarea from '@/shared/components/UaTextarea.vue';
import { mapToValidationErrors } from '@/shared/validation/validationErrors';
import { useLocationsStore } from '@/stores/LocationsStore';
import type { SelectOption, SelectValue } from '@/types/select';
import { DateTime } from 'luxon';
import { RRule } from 'rrule';
import { computed, onMounted, ref, watch } from 'vue';
import {
  buildCreateAssignmentPayload,
  createInitialAssignmentFormData,
  defaultAssignmentColor,
  normalizeAssignmentFormTimes,
  resolveShiftEntryLinksFromAssignmentEntry,
  validateAssignmentFormData,
  type AssignmentFormData,
} from './calendarSchedulingAssignmentForm';
import { resolveShiftSeriesLinksFromAssignmentSeries } from './calendarSchedulingAssignmentSeriesLinks';
import {
  createAssignmentEntry,
  createAssignmentSeries,
  deleteAssignmentEntry,
  deleteAssignmentSeries,
  expireAssignmentEntry,
  expireAssignmentSeries,
  loadAssignmentEntry,
  loadAssignmentSeriesById,
  updateAssignmentEntry,
  updateAssignmentSeries,
} from './calendarSchedulingAssignmentApi';
import { formatUserOptionLabel, repeatOptions } from './calendarSchedulingShiftForm';
import CalendarSchedulingShiftDetailsPanel from './CalendarSchedulingShiftDetailsPanel.vue';
import { useAssignmentDefinitionOptions } from './useAssignmentDefinitionOptions';
import { useSchedulingUsersStore } from './useSchedulingUsersStore';
import {
  canAddAssignmentLinkToShift,
  getSchedulingLifecycleCapabilities,
  isSchedulingLinkable,
  isSchedulingPublished,
} from './schedulingLifecycle';
import { resolveSchedulingTimeZoneId } from './schedulingTimeZone';
import { createLatestRequestGuard } from './latestRequestGuard';
import { parsePositiveInteger } from './calendarSchedulingShiftIds';
import { parseStringArray } from './calendarSchedulingLinkMappers';
import { formatTimeOptionRange, normalizeTimeOptionValue, parseFormDateTime, timeOptions } from './schedulingDateTime';

const props = defineProps<{
  mode?: 'create' | 'view' | 'edit';
  editScope?: 'event' | 'series';
  assignmentEntryId?: number;
  assignmentSeriesId?: number;
  initialDate?: string;
  initialAssignmentDefinitionId?: number;
  initialShiftEntryIds?: number[];
  timeZone?: string;
}>();

const emit = defineEmits<{
  close: [];
}>();

const calendarStore = useCalendarStore();
const locationsStore = useLocationsStore();
const schedulingUsersStore = useSchedulingUsersStore();

type AssignmentDetailTabId = 'details' | 'edit' | 'delete';
type AssignmentOpenScope = 'event' | 'series';
type AssignmentLoadState = 'idle' | 'loading' | 'loaded' | 'loadError';
type AssignmentDetailRow = {
  label: string;
  value: string;
  recurrenceRule?: string | null;
  recurrenceStartDate?: string | null;
};

const publishedAssignmentMessage =
  'This assignment has been published, and cannot be edited or deleted, only cancelled';
const publishedShiftLinkError = 'Shift already published. To link a new assignment, please create a new shift.';
const publishedShiftLinkMessage =
  'This assignment is linked to a published shift. New links to published shifts cannot be added.';

const isSaving = ref(false);
const isLoadingShiftOptions = ref(false);
const apiError = ref('');
const assignmentLoadError = ref('');
const assignmentLoadState = ref<AssignmentLoadState>(props.mode === 'create' ? 'loaded' : 'idle');
const isLoadingAssignment = computed(() => assignmentLoadState.value === 'loading');
const assignmentStatusTypeCode = ref<string | null>(null);
const formErrors = ref<Record<string, string>>({});
const recurrenceError = ref('');
const shiftSeries = ref<ShiftSeriesResponse[]>([]);
const shiftEntries = ref<ShiftEntryResponse[]>([]);
const users = ref<UserResponse[]>([]);
const allUsers = ref<UserResponse[]>([]);
const shiftOptionsRequestGuard = createLatestRequestGuard();
const usersRequestGuard = createLatestRequestGuard();
const hasAppliedInitialShiftEntrySelection = ref(false);
const hasAppliedInitialAssignmentDefinitionSelection = ref(false);
const selectedShiftSeriesId = ref<number | null>(null);
const selectedShiftEntryId = ref<number | null>(null);
const modalMode = ref<'create' | 'view' | 'edit'>(props.mode ?? 'create');
const activeTab = ref<AssignmentDetailTabId>('details');
const selectedOpenScope = ref<AssignmentOpenScope | null>(getInitialOpenScope());

const appBarLocationId = computed<number | null>(() => {
  const candidate = locationsStore.selectedLocationId;

  if (candidate === '' || candidate == null) {
    return null;
  }

  const parsedLocationId = Number(candidate);
  return Number.isFinite(parsedLocationId) ? parsedLocationId : null;
});
const formData = ref<AssignmentFormData>(createInitialFormData(props.initialDate));
const activeLocationId = computed<number | null>(() => parsePositiveInteger(formData.value.locationId));
const timeZoneId = computed(() =>
  resolveSchedulingTimeZoneId(
    activeLocationId.value ? locationsStore.entitiesMap[activeLocationId.value]?.timezone : undefined,
    props.timeZone,
  ),
);
const eventBelongsToSeries = computed(() => Boolean(props.assignmentEntryId && props.assignmentSeriesId));
const shouldShowOpenScopeChoice = computed(() => eventBelongsToSeries.value && selectedOpenScope.value === null);
const shouldShowDetailTabs = computed(() => props.mode === 'view' && !shouldShowOpenScopeChoice.value);
const isDeleteTab = computed(() => shouldShowDetailTabs.value && activeTab.value === 'delete');
const isReadOnly = computed(() => modalMode.value === 'view');
const isEditMode = computed(() => modalMode.value === 'edit');
const shouldShowReadOnlyDetails = computed(
  () => isReadOnly.value && !isDeleteTab.value && activeTab.value === 'details',
);
const isSeriesScope = computed(() => selectedOpenScope.value === 'series');
const shouldShowRecurrenceFields = computed(() => !isEditMode.value || isSeriesScope.value);
const isBusy = computed(() => isSaving.value || isLoadingAssignment.value);
const fieldsDisabled = computed(() => isBusy.value || isReadOnly.value);
const assignmentEntityLabel = computed(() => (isSeriesScope.value ? 'Assignment Series' : 'Assignment'));
const assignmentLinkedToPublishedShift = computed(() => {
  const hasPublishedEntryLink = (formData.value.shiftEntryLinks ?? []).some((link) =>
    typeof link.shiftEntryId === 'number'
      ? isSchedulingPublished(getShiftEntry(link.shiftEntryId)?.statusTypeCode)
      : false,
  );
  const hasPublishedSeriesLink = (formData.value.shiftSeriesLinks ?? []).some((link) =>
    typeof link.shiftSeriesId === 'number'
      ? isSchedulingPublished(getShiftSeries(link.shiftSeriesId)?.statusTypeCode)
      : false,
  );

  return hasPublishedEntryLink || hasPublishedSeriesLink;
});
const assignmentLifecycle = computed(() => getSchedulingLifecycleCapabilities(assignmentStatusTypeCode.value));
const visibleAssignmentDetailTabs = computed<Array<{ id: AssignmentDetailTabId; label: string }>>(() => [
  { id: 'details', label: 'Details' },
  ...(assignmentLoadState.value === 'loaded' && assignmentLifecycle.value.canEdit
    ? [{ id: 'edit' as const, label: 'Edit' }]
    : []),
  ...(assignmentLoadState.value === 'loaded' &&
  (assignmentLifecycle.value.canDelete || assignmentLifecycle.value.canCancel)
    ? [{ id: 'delete' as const, label: assignmentLifecycle.value.canCancel ? 'Cancel' : 'Delete' }]
    : []),
]);
const modalTitle = computed(() => {
  if (shouldShowOpenScopeChoice.value) {
    return 'Open Assignment';
  }

  if (isDeleteTab.value) {
    return `${assignmentLifecycle.value.canCancel ? 'Cancel' : 'Delete'} ${assignmentEntityLabel.value}`;
  }

  if (isReadOnly.value) {
    return `${assignmentEntityLabel.value} Details`;
  }

  if (isEditMode.value) {
    return `Edit ${assignmentEntityLabel.value}`;
  }

  return 'Add Assignment';
});
const calendarContextDate = computed(() =>
  DateTime.fromISO(formData.value.date || props.initialDate || '', { zone: timeZoneId.value }),
);
const {
  assignmentDefinitions,
  assignmentDefinitionOptions,
  isLoadingAssignmentDefinitions,
  loadAssignmentDefinitions,
  selectedAssignmentDefinition,
} = useAssignmentDefinitionOptions({
  activeLocationId,
  timeZoneId,
  contextDate: calendarContextDate,
  selectedAssignmentDefinitionId: computed(() => formData.value.assignmentDefinitionId),
  onError: (message) => {
    apiError.value = message;
  },
  onLoaded: applyInitialSelections,
});
const locationOptionsWithSelected = computed(() => {
  const locationId = activeLocationId.value;
  if (!locationId || locationsStore.selectOptions.some((option) => Number(option.code) === locationId)) {
    return locationsStore.selectOptions;
  }

  return [{ code: locationId, description: 'Unknown location' }, ...locationsStore.selectOptions];
});
const hasAssignmentRecurrence = computed(() => isSeriesScope.value || formData.value.repeatMode === 'custom');
const shouldShowSingleShiftLinks = computed(() => !hasAssignmentRecurrence.value);
const shouldShowRecurringShiftLinks = computed(() => hasAssignmentRecurrence.value);
const allUsersById = computed(() => new Map([...users.value, ...allUsers.value].map((user) => [user.id, user])));
const selectedShiftSeriesIds = computed(
  () => new Set((formData.value.shiftSeriesLinks ?? []).map((link) => link.shiftSeriesId)),
);
const selectedShiftEntryIds = computed(
  () => new Set((formData.value.shiftEntryLinks ?? []).map((link) => link.shiftEntryId)),
);
const shiftSeriesOptions = computed<ShiftOption[]>(() =>
  shiftSeries.value
    .filter((series) => typeof series.id === 'number')
    .filter((series) => isSchedulingLinkable(series.statusTypeCode))
    .filter((series) => !selectedShiftSeriesIds.value.has(series.id as number))
    .map((series) => ({
      code: series.id as number,
      description: formatShiftSeriesTitle(series),
      subtitle: formatShiftSeriesSubtitle(series),
    }))
    .sort((left, right) => left.description.localeCompare(right.description)),
);
const shiftEntryOptions = computed<ShiftOption[]>(() =>
  shiftEntries.value
    .filter((entry) => typeof entry.id === 'number')
    .filter((entry) => isSchedulingLinkable(entry.statusTypeCode))
    .filter((entry) => !selectedShiftEntryIds.value.has(entry.id as number))
    .map((entry) => ({
      code: entry.id as number,
      description: formatShiftEntryTitle(entry),
    }))
    .sort((left, right) => left.description.localeCompare(right.description)),
);
const assignmentDetailRows = computed<AssignmentDetailRow[]>(() => {
  const rows: AssignmentDetailRow[] = [
    {
      label: 'Location',
      value: formatLocation(formData.value.locationId),
    },
    {
      label: 'Assignment Type',
      value: selectedAssignmentDefinition.value?.name || formData.value.title || 'None',
    },
    { label: 'Capacity', value: String(formData.value.capacity ?? 'None') },
    { label: 'Date', value: formatAssignmentDetailDate(formData.value.date) },
    { label: 'Time', value: formatTimeOptionRange(formData.value.startTime, formData.value.endTime) },
  ];

  if (formData.value.recurrenceRule) {
    rows.push({
      label: 'Repeat',
      value: '',
      recurrenceRule: formData.value.recurrenceRule,
      recurrenceStartDate: formData.value.date ?? null,
    });
  } else {
    rows.push({ label: 'Repeat', value: 'Never' });
  }

  if (shouldShowRecurringShiftLinks.value) {
    rows.push({
      label: 'Link Recurring Shift(s)',
      value: formatAssignmentDetailShiftSeriesLinks(),
    });
  }

  if (shouldShowSingleShiftLinks.value) {
    rows.push({
      label: 'Link Shift(s)',
      value: formatAssignmentDetailShiftEntryLinks(),
    });
  }

  rows.push({ label: 'Notes', value: formData.value.notes?.trim() || 'None' });

  return rows;
});

interface ShiftOption extends SelectOption {
  subtitle?: string;
}

interface UserOption extends SelectOption {
  code: string;
}

onMounted(() => {
  void Promise.all([loadAssignmentDefinitions(), loadShiftOptions(), loadUsers()]);
  if (!shouldShowOpenScopeChoice.value) {
    void loadInitialAssignment();
  }
});

watch(
  () => [props.initialDate, props.assignmentEntryId, props.assignmentSeriesId, props.mode, props.editScope] as const,
  ([initialDate]) => {
    selectedOpenScope.value = getInitialOpenScope();
    modalMode.value = props.mode ?? 'create';
    activeTab.value = 'details';
    formData.value = createInitialFormData(initialDate);
    hasAppliedInitialShiftEntrySelection.value = false;
    hasAppliedInitialAssignmentDefinitionSelection.value = false;
    applyInitialSelections();
    apiError.value = '';
    recurrenceError.value = '';
    formErrors.value = {};
    if (!shouldShowOpenScopeChoice.value) {
      void loadInitialAssignment();
    }
  },
);

watch(activeLocationId, () => {
  void Promise.all([loadAssignmentDefinitions(), loadUsers()]);
});

watch(
  () => [activeLocationId.value, formData.value.date, timeZoneId.value],
  () => {
    if (assignmentLoadState.value !== 'loading') {
      void loadShiftOptions();
    }
  },
);

watch(
  () => props.editScope,
  (editScope) => {
    selectedOpenScope.value = editScope ?? getInitialOpenScope();
    applyScopeRestrictions();
  },
);

async function selectOpenScope(scope: AssignmentOpenScope) {
  selectedOpenScope.value = scope;
  activeTab.value = 'details';
  formData.value = createInitialFormData(props.initialDate);
  apiError.value = '';
  formErrors.value = {};
  await loadInitialAssignment();
}

watch(
  () => formData.value.repeatMode,
  (value, previousValue) => {
    if (isSeriesScope.value || value === previousValue) {
      return;
    }

    if (value === 'never') {
      formData.value.recurrenceRule = null;
      formData.value.shiftSeriesLinks = [];
      selectedShiftSeriesId.value = null;
      recurrenceError.value = '';
    } else if (value === 'custom') {
      formData.value.shiftEntryLinks = [];
      selectedShiftEntryId.value = null;
    }
  },
);

async function loadShiftOptions() {
  const requestId = shiftOptionsRequestGuard.begin();
  const locationId = activeLocationId.value;
  const dateRange = getShiftOptionsDateRange();
  shiftSeries.value = [];
  shiftEntries.value = [];
  if (!locationId || !dateRange) {
    isLoadingShiftOptions.value = false;
    return;
  }

  isLoadingShiftOptions.value = true;

  try {
    const queryParams = { LocationId: locationId, ...dateRange };
    const [seriesResult, entryResult] = [
      getApiSchedulingShiftsSeries(queryParams, { options: { immediate: false } }),
      getApiSchedulingShiftsEntries(queryParams, { options: { immediate: false } }),
    ];

    await Promise.all([seriesResult.execute(), entryResult.execute()]);

    if (!shiftOptionsRequestGuard.isCurrent(requestId)) {
      return;
    }

    if (seriesResult.error.value || entryResult.error.value) {
      apiError.value =
        seriesResult.error.value?.message || entryResult.error.value?.message || 'Failed to load shift options.';
      return;
    }

    shiftSeries.value = seriesResult.data.value ?? [];
    shiftEntries.value = entryResult.data.value ?? [];
    applyInitialSelections();
  } catch (error: unknown) {
    if (shiftOptionsRequestGuard.isCurrent(requestId)) {
      apiError.value = error instanceof Error ? error.message : 'Failed to load shift options.';
    }
  } finally {
    if (shiftOptionsRequestGuard.isCurrent(requestId)) {
      isLoadingShiftOptions.value = false;
    }
  }
}

function getShiftOptionsDateRange() {
  const localDay = DateTime.fromISO(formData.value.date ?? '', { zone: timeZoneId.value }).startOf('day');
  if (!localDay.isValid) {
    return null;
  }

  return {
    StartAtUtc: localDay.toUTC().toISO(),
    EndAtUtc: localDay.plus({ days: 1 }).toUTC().toISO(),
  };
}

async function loadUsers() {
  const requestId = usersRequestGuard.begin();
  const locationId = activeLocationId.value;
  if (!locationId) {
    users.value = [];
    allUsers.value = [];
    return;
  }

  try {
    const [locationUsers, cachedAllUsers] = await Promise.all([
      schedulingUsersStore.ensureUsersForLocation(locationId),
      schedulingUsersStore.ensureAllUsers(),
    ]);

    if (usersRequestGuard.isCurrent(requestId)) {
      users.value = locationUsers;
      allUsers.value = cachedAllUsers;
    }
  } catch (error: unknown) {
    if (!usersRequestGuard.isCurrent(requestId) || isRequestAbortError(error)) {
      return;
    }

    apiError.value = error instanceof Error ? error.message : 'Failed to load users.';
  }
}

function isRequestAbortError(error: unknown) {
  if (!error) {
    return false;
  }

  if (error instanceof DOMException && error.name === 'AbortError') {
    return true;
  }

  if (error instanceof Error) {
    return error.name === 'AbortError' || error.message.toLowerCase().includes('aborted');
  }

  return false;
}

function updateField<TKey extends keyof AssignmentFormData>(key: TKey, value: AssignmentFormData[TKey]) {
  formData.value = {
    ...formData.value,
    [key]: value,
  };
}

function updateSelectField<TKey extends keyof AssignmentFormData>(key: TKey, value: SelectValue | undefined) {
  updateField(key, (value ?? null) as AssignmentFormData[TKey]);
}

function createInitialFormData(initialDate?: string): AssignmentFormData {
  return {
    ...createInitialAssignmentFormData(initialDate),
    locationId: appBarLocationId.value,
  };
}

function updateLocation(value: SelectValue | undefined) {
  const locationId = parsePositiveInteger(value);
  if (locationId === activeLocationId.value) {
    return;
  }

  hasAppliedInitialAssignmentDefinitionSelection.value = true;
  formData.value = {
    ...formData.value,
    locationId,
    assignmentDefinitionId: undefined,
    categoryId: undefined,
    subCategoryId: undefined,
    capacity: 1,
    shiftEntryLinks: [],
    shiftSeriesLinks: [],
  };
  selectedShiftEntryId.value = null;
  selectedShiftSeriesId.value = null;
}

function updateAssignmentDefinition(value: SelectValue | undefined) {
  const assignmentDefinitionId = typeof value === 'number' ? value : Number(value);
  const assignmentDefinition = assignmentDefinitions.value.find((candidate) => candidate.id === assignmentDefinitionId);

  updateField('assignmentDefinitionId', Number.isFinite(assignmentDefinitionId) ? assignmentDefinitionId : undefined);
  updateField('categoryId', assignmentDefinition?.categoryId);
  updateField('subCategoryId', assignmentDefinition?.subCategoryId);
  updateField('capacity', assignmentDefinition?.defaultCapacity ?? 1);
  updateField('color', assignmentDefinition?.color?.trim() || defaultAssignmentColor);
  updateField('locationId', assignmentDefinition?.locationId ?? null);

  if (assignmentDefinition?.defaultStartTime) {
    updateField('startTime', normalizeTimeOptionValue(assignmentDefinition.defaultStartTime));
  }

  if (assignmentDefinition?.defaultEndTime) {
    updateField('endTime', normalizeTimeOptionValue(assignmentDefinition.defaultEndTime));
  }
}

function applyInitialSelections() {
  if (!applyInitialAssignmentDefinition()) {
    return;
  }

  if (hasAppliedInitialShiftEntrySelection.value) {
    return;
  }

  const hasExplicitInitialShiftEntryIds = props.initialShiftEntryIds !== undefined;
  const shiftEntryIds = getInitialShiftEntryIds();

  if (shiftEntryIds.length && !initialShiftEntriesAreLoaded(shiftEntryIds)) {
    return;
  }

  if (hasExplicitInitialShiftEntryIds) {
    hasAppliedInitialShiftEntrySelection.value = true;
  }

  if (!shiftEntryIds.length) {
    return;
  }

  const existingLinks = (formData.value.shiftEntryLinks ?? []).flatMap((link) =>
    typeof link.shiftEntryId === 'number'
      ? [{ ...link, shiftEntryId: link.shiftEntryId, assignedUserIds: link.assignedUserIds ?? [] }]
      : [],
  );
  const existingLinksById = new Map(existingLinks.map((link) => [link.shiftEntryId, link]));
  const mergedShiftEntryIds = [...new Set([...existingLinks.map((link) => link.shiftEntryId), ...shiftEntryIds])];
  updateField('repeatMode', 'never');
  updateField(
    'shiftEntryLinks',
    mergedShiftEntryIds.map(
      (shiftEntryId) =>
        existingLinksById.get(shiftEntryId) ?? {
          shiftEntryId,
          assignedUserIds: getShiftEntryUserIds(shiftEntryId),
        },
    ),
  );
  updateField('shiftSeriesLinks', []);
  hasAppliedInitialShiftEntrySelection.value = true;
}

function applyInitialAssignmentDefinition() {
  const assignmentDefinitionId = props.initialAssignmentDefinitionId;
  if (!assignmentDefinitionId || hasAppliedInitialAssignmentDefinitionSelection.value) {
    return true;
  }

  if (assignmentDefinitionOptions.value.some((candidate) => Number(candidate.code) === assignmentDefinitionId)) {
    updateAssignmentDefinition(assignmentDefinitionId);
    hasAppliedInitialAssignmentDefinitionSelection.value = true;
    return true;
  }

  const assignmentDefinition = assignmentDefinitions.value.find((candidate) => candidate.id === assignmentDefinitionId);
  const effectiveDate = assignmentDefinition?.effectiveDateUtc
    ? DateTime.fromISO(assignmentDefinition.effectiveDateUtc, { setZone: true })
        .setZone(timeZoneId.value)
        .startOf('day')
    : null;

  if (effectiveDate?.isValid && effectiveDate > calendarContextDate.value.startOf('day')) {
    apiError.value = `Assignment ${assignmentDefinition?.name || 'type'} is not effective until ${effectiveDate.toLocaleString(
      DateTime.DATE_FULL,
    )}`;
  }
  return false;
}

function getInitialShiftEntryIds() {
  if (props.initialShiftEntryIds !== undefined) {
    return props.initialShiftEntryIds;
  }

  if (!props.initialAssignmentDefinitionId) {
    return [];
  }

  return shiftEntries.value.flatMap((entry) =>
    typeof entry.id === 'number' && isSchedulingLinkable(entry.statusTypeCode) ? [entry.id] : [],
  );
}

function initialShiftEntriesAreLoaded(shiftEntryIds: number[]) {
  const loadedShiftEntryIds = new Set(
    shiftEntries.value.flatMap((entry) => (typeof entry.id === 'number' ? [entry.id] : [])),
  );

  return shiftEntryIds.every((shiftEntryId) => loadedShiftEntryIds.has(shiftEntryId));
}

function updateSelectedShiftSeries(value: SelectValue | undefined) {
  selectedShiftSeriesId.value = parsePositiveInteger(value);
  addSelectedShiftSeries();
}

function updateSelectedShiftEntry(value: SelectValue | undefined) {
  selectedShiftEntryId.value = parsePositiveInteger(value);
  addSelectedShiftEntry();
}

function addSelectedShiftSeries() {
  const shiftSeriesId = selectedShiftSeriesId.value;
  if (!shiftSeriesId || selectedShiftSeriesIds.value.has(shiftSeriesId)) {
    return;
  }

  if (!canAddAssignmentLinkToShift(getShiftSeries(shiftSeriesId)?.statusTypeCode)) {
    apiError.value = publishedShiftLinkError;
    selectedShiftSeriesId.value = null;
    return;
  }

  const nextLinks = [
    ...(formData.value.shiftSeriesLinks ?? []),
    {
      shiftSeriesId,
      assignedUserIds: getShiftSeriesUserIds(shiftSeriesId),
    },
  ];
  updateField('shiftSeriesLinks', nextLinks);
  selectedShiftSeriesId.value = null;
}

function addSelectedShiftEntry() {
  const shiftEntryId = selectedShiftEntryId.value;
  if (!shiftEntryId || selectedShiftEntryIds.value.has(shiftEntryId)) {
    return;
  }

  if (!canAddAssignmentLinkToShift(getShiftEntry(shiftEntryId)?.statusTypeCode)) {
    apiError.value = publishedShiftLinkError;
    selectedShiftEntryId.value = null;
    return;
  }

  const nextLinks = [
    ...(formData.value.shiftEntryLinks ?? []),
    {
      shiftEntryId,
      assignedUserIds: getShiftEntryUserIds(shiftEntryId),
    },
  ];
  updateField('shiftEntryLinks', nextLinks);
  selectedShiftEntryId.value = null;
}

function removeShiftSeriesLink(index: number) {
  const nextLinks = [...(formData.value.shiftSeriesLinks ?? [])];
  nextLinks.splice(index, 1);
  updateField('shiftSeriesLinks', nextLinks);
}

function removeShiftEntryLink(index: number) {
  const nextLinks = [...(formData.value.shiftEntryLinks ?? [])];
  nextLinks.splice(index, 1);
  updateField('shiftEntryLinks', nextLinks);
}

function updateShiftSeriesLinkUsers(index: number, value: SelectValue | undefined) {
  const nextLinks = [...(formData.value.shiftSeriesLinks ?? [])];
  const link = nextLinks[index];
  if (!link) {
    return;
  }

  nextLinks[index] = { ...link, assignedUserIds: parseStringArray(value) };
  updateField('shiftSeriesLinks', nextLinks);
}

function updateShiftEntryLinkUsers(index: number, value: SelectValue | undefined) {
  const nextLinks = [...(formData.value.shiftEntryLinks ?? [])];
  const link = nextLinks[index];
  if (!link) {
    return;
  }

  nextLinks[index] = { ...link, assignedUserIds: parseStringArray(value) };
  updateField('shiftEntryLinks', nextLinks);
}

function handleClose() {
  if (!isBusy.value) {
    emit('close');
  }
}

function selectTab(tabId: AssignmentDetailTabId) {
  if (isBusy.value) {
    return;
  }

  if (!visibleAssignmentDetailTabs.value.some((tab) => tab.id === tabId)) {
    return;
  }

  activeTab.value = tabId;
  modalMode.value = tabId === 'edit' ? 'edit' : 'view';
  apiError.value = '';
  formErrors.value = {};
  applyScopeRestrictions();
}

function handleRecurrenceChange(value: string | null) {
  recurrenceError.value = '';
  updateField('recurrenceRule', value);
  if (isSeriesScope.value) {
    return;
  }

  if (value) {
    updateField('shiftEntryLinks', []);
    selectedShiftEntryId.value = null;
  } else {
    updateField('shiftSeriesLinks', []);
    selectedShiftSeriesId.value = null;
  }
}

function formatShiftSeriesTitle(series: ShiftSeriesResponse) {
  return series.title?.trim() || `Shift series ${series.id}`;
}

function formatShiftSeriesSubtitle(series: ShiftSeriesResponse) {
  const parts = [
    formatRRuleText(series.recurrenceRule),
    formatDateTimeRange(series.startAtUtc, series.endAtUtc, series.timeZoneId),
    formatUserIds(series.userIds),
  ].filter(Boolean);

  return parts.join('\n');
}

function formatShiftSeriesLinkTitle(shiftSeriesId: number) {
  const series = getShiftSeries(shiftSeriesId);
  return series?.title?.trim() || 'Shift series';
}

function formatShiftSeriesLinkDetails(shiftSeriesId: number) {
  const series = getShiftSeries(shiftSeriesId);
  return series ? formatShiftSeriesSubtitle(series) : '';
}

function formatShiftEntryLinkDetails(shiftEntryId: number) {
  const entry = getShiftEntry(shiftEntryId);
  return entry ? formatShiftEntryTitle(entry) : '';
}

function formatShiftEntryTitle(entry: ShiftEntryResponse) {
  return `${formatShiftEntryUsers(entry)} - ${formatShiftEntryDateTimeRange(entry)}`;
}

function formatRRuleText(value?: string | null) {
  if (!value) {
    return '';
  }

  try {
    return RRule.fromString(value).toText();
  } catch {
    return value;
  }
}

function formatDateTimeRange(startAtUtc?: string | null, endAtUtc?: string | null, eventTimeZoneId?: string | null) {
  if (!startAtUtc) {
    return '';
  }

  const zone = eventTimeZoneId || timeZoneId.value;
  const start = DateTime.fromISO(startAtUtc, { setZone: true }).setZone(zone);
  const end = endAtUtc ? DateTime.fromISO(endAtUtc, { setZone: true }).setZone(zone) : null;

  if (!start.isValid) {
    return '';
  }

  const date = start.toLocaleString(DateTime.DATE_MED);
  const startTime = start.toLocaleString(DateTime.TIME_SIMPLE);
  const endTime = end?.isValid ? end.toLocaleString(DateTime.TIME_SIMPLE) : '';

  return endTime ? `${date}, ${startTime} - ${endTime}` : `${date}, ${startTime}`;
}

function formatUserIds(userIds?: string[]) {
  return userIds?.length ? `Users: ${userIds.map(formatUserId).join(', ')}` : '';
}

function formatShiftEntryUsers(entry: ShiftEntryResponse) {
  const userIds = entry.userIds ?? [];

  if (!userIds.length) {
    return 'Unassigned';
  }

  return userIds
    .map((userId) => {
      const user = allUsersById.value.get(userId);
      return user ? formatUserOptionLabel(user) : userId;
    })
    .join(', ');
}

function formatShiftEntryDateTimeRange(entry: ShiftEntryResponse) {
  if (!entry.startAtUtc) {
    return '';
  }

  const zone = entry.timeZoneId || timeZoneId.value;
  const start = DateTime.fromISO(entry.startAtUtc, { setZone: true }).setZone(zone);
  const end = entry.endAtUtc ? DateTime.fromISO(entry.endAtUtc, { setZone: true }).setZone(zone) : null;

  if (!start.isValid) {
    return '';
  }

  const startDate = formatShiftEntryDate(start);
  const startTime = start.toLocaleString(DateTime.TIME_SIMPLE);

  if (!end?.isValid) {
    return `${startDate}, ${startTime}`;
  }

  const endTime = end.toLocaleString(DateTime.TIME_SIMPLE);

  if (start.hasSame(end, 'day')) {
    return `${startDate}, ${startTime} - ${endTime}`;
  }

  return `${startDate}, ${startTime} - ${formatShiftEntryDate(end)}, ${endTime}`;
}

function formatShiftEntryDate(value: DateTime) {
  return value.toFormat('LLL d yyyy');
}

function formatAssignmentDetailDate(value?: string) {
  if (!value) {
    return 'Unknown';
  }

  const dateTime = DateTime.fromISO(value, { zone: timeZoneId.value });
  return dateTime.isValid ? dateTime.toLocaleString(DateTime.DATE_FULL) : 'Unknown';
}

function formatLocation(locationId: unknown) {
  const parsedLocationId = parsePositiveInteger(locationId);
  if (!parsedLocationId) {
    return 'Unknown location';
  }

  const option = locationsStore.selectOptions.find((candidate) => Number(candidate.code) === parsedLocationId);
  return option?.description || 'Unknown location';
}

function formatAssignmentDetailShiftSeriesLinks() {
  const links = formData.value.shiftSeriesLinks ?? [];
  if (!links.length) {
    return 'None';
  }

  return links
    .map((link, index) => {
      const shiftSeriesId = link.shiftSeriesId;
      const parts = [
        typeof shiftSeriesId === 'number' ? formatShiftSeriesLinkTitle(shiftSeriesId) : `Recurring Shift ${index + 1}`,
        typeof shiftSeriesId === 'number' ? formatShiftSeriesLinkDetails(shiftSeriesId) : '',
        formatAssignmentDetailLinkUsers(link.assignedUserIds),
      ].filter(Boolean);

      return parts.join('\n');
    })
    .join('\n\n');
}

function formatAssignmentDetailShiftEntryLinks() {
  const links = formData.value.shiftEntryLinks ?? [];
  if (!links.length) {
    return 'None';
  }

  return links
    .map((link, index) => {
      const shiftEntryId = link.shiftEntryId;
      const parts = [
        typeof shiftEntryId === 'number' ? 'Shift' : `Shift ${index + 1}`,
        typeof shiftEntryId === 'number' ? formatShiftEntryLinkDetails(shiftEntryId) : '',
        formatAssignmentDetailLinkUsers(link.assignedUserIds),
      ].filter(Boolean);

      return parts.join('\n');
    })
    .join('\n\n');
}

function formatAssignmentDetailLinkUsers(userIds?: string[] | null) {
  return userIds?.length ? `Users: ${userIds.map(formatUserId).join(', ')}` : 'Users: None';
}

function formatUserId(userId: string) {
  const user = allUsersById.value.get(userId);
  return user ? formatUserOptionLabel(user) : userId;
}

function getShiftSeries(shiftSeriesId: number) {
  return shiftSeries.value.find((series) => series.id === shiftSeriesId);
}

function getShiftEntry(shiftEntryId: number) {
  return shiftEntries.value.find((entry) => entry.id === shiftEntryId);
}

function getShiftSeriesUserIds(shiftSeriesId: number) {
  return (
    getShiftSeries(shiftSeriesId)?.userIds ??
    formData.value.shiftSeriesLinks?.find((link) => link.shiftSeriesId === shiftSeriesId)?.assignedUserIds ??
    []
  );
}

function getShiftEntryUserIds(shiftEntryId: number) {
  return (
    getShiftEntry(shiftEntryId)?.userIds ??
    formData.value.shiftEntryLinks?.find((link) => link.shiftEntryId === shiftEntryId)?.assignedUserIds ??
    []
  );
}

function getShiftSeriesUserOptions(shiftSeriesId: number): UserOption[] {
  return getUserOptions(getShiftSeriesUserIds(shiftSeriesId));
}

function getShiftEntryUserOptions(shiftEntryId: number): UserOption[] {
  return getUserOptions(getShiftEntryUserIds(shiftEntryId));
}

function getUserOptions(userIds: string[]): UserOption[] {
  return userIds.map((userId) => ({
    code: userId,
    description: formatUserId(userId),
  }));
}

function getLinkUserError(type: 'shiftSeriesLinks' | 'shiftEntryLinks', index: number) {
  return (
    formErrors.value[`${type}.${index}.assignedUserIds`] ||
    formErrors.value[`${type}.${index}.userIds`] ||
    formErrors.value[type] ||
    ''
  );
}

function getShiftOptionDescription(item: ShiftOption | { raw?: ShiftOption }) {
  return getShiftOptionFromSlotItem(item).description;
}

function getShiftOptionSubtitle(item: ShiftOption | { raw?: ShiftOption }) {
  return getShiftOptionFromSlotItem(item).subtitle;
}

function getShiftOptionFromSlotItem(item: ShiftOption | { raw?: ShiftOption }): ShiftOption {
  if ('description' in item) {
    return item;
  }

  return item.raw ?? { code: '', description: '' };
}

function getCustomListItemProps(itemProps: Record<string, unknown>) {
  const result = { ...itemProps };
  delete result.title;
  delete result.subtitle;
  return result;
}

function validateForm(): AssignmentFormData | null {
  formErrors.value = {};
  formData.value = normalizeAssignmentFormTimes(formData.value);

  const result = validateAssignmentFormData(formData.value, {
    timeZoneId: timeZoneId.value,
    recurrenceError: recurrenceError.value,
  });

  if (!result.data) {
    formErrors.value = result.errors;
    return null;
  }

  return result.data;
}

async function handleSave() {
  if (isReadOnly.value) {
    return;
  }

  const validated = validateForm();
  if (!validated) {
    apiError.value = 'Could not save the assignment. Check the highlighted fields.';
    return;
  }

  const payload = buildCreateAssignmentPayload({
    formData: validated,
    timeZoneId: timeZoneId.value,
    locationId: validated.locationId ?? activeLocationId.value,
    assignmentOptions: assignmentDefinitionOptions.value,
  });
  if (!payload) {
    apiError.value = 'Could not resolve the selected assignment date, time, and location.';
    return;
  }

  isSaving.value = true;
  apiError.value = '';

  try {
    if (isEditMode.value && isSeriesScope.value && payload.kind !== 'series') {
      apiError.value = 'Could not build a recurring assignment update.';
      return;
    }

    const saveResult =
      isEditMode.value && isSeriesScope.value && props.assignmentSeriesId && payload.kind === 'series'
        ? await updateAssignmentSeries(props.assignmentSeriesId, payload.body)
        : isEditMode.value && props.assignmentEntryId && payload.kind === 'entry'
          ? await updateAssignmentEntry(props.assignmentEntryId, payload.body)
          : payload.kind === 'series'
            ? await createAssignmentSeries(payload.body)
            : await createAssignmentEntry(payload.body);

    if (saveResult.error.value) {
      if (applyServerValidationErrors(saveResult.error.value.data)) {
        apiError.value = 'Could not save the assignment. Check the highlighted fields.';
        return;
      }

      apiError.value = resolveAssignmentSaveError(
        saveResult.error.value,
        isEditMode.value
          ? 'Failed to update assignment.'
          : payload.kind === 'series'
            ? 'Failed to create assignment series.'
            : 'Failed to create assignment.',
      );
      return;
    }

    const savedId = saveResult.data.value?.id;
    if (!savedId) {
      apiError.value = 'Assignment was saved but the response did not include an id.';
      return;
    }

    calendarStore.refresh();
    emit('close');
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'An unexpected error occurred.';
  } finally {
    isSaving.value = false;
  }
}

function applyServerValidationErrors(rawError: unknown) {
  const mapped = mapToValidationErrors(rawError);
  if (!mapped) {
    return false;
  }

  formErrors.value = normalizeAssignmentServerValidationErrors(mapped);
  return Object.keys(formErrors.value).length > 0;
}

function resolveAssignmentSaveError(error: unknown, fallback: string) {
  const responseError = error as { message?: unknown; data?: { detail?: unknown; title?: unknown } };
  const messages = [responseError.data?.detail, responseError.message, responseError.data?.title].filter(
    (value): value is string => typeof value === 'string' && Boolean(value.trim()),
  );
  return messages[0] ?? fallback;
}

function normalizeAssignmentServerValidationErrors(errors: Record<string, string>) {
  return Object.entries(errors).reduce<Record<string, string>>((result, [fieldName, message]) => {
    result[mapAssignmentServerValidationField(fieldName)] = message;
    return result;
  }, {});
}

function mapAssignmentServerValidationField(fieldName: string) {
  const normalizedPath = fieldName
    .replace(/\[(\d+)\]/g, '.$1')
    .split('.')
    .filter(Boolean)
    .map((part) => (part.match(/^\d+$/) ? part : part.charAt(0).toLowerCase() + part.slice(1)))
    .join('.');
  const normalized = normalizedPath.toLowerCase();

  if (normalized === 'startatutc') {
    return 'startTime';
  }

  if (normalized === 'endatutc') {
    return 'endTime';
  }

  if (normalized === 'shiftentryids') {
    return 'shiftEntryLinks';
  }

  if (normalized === 'shiftseriesids') {
    return 'shiftSeriesLinks';
  }

  return normalizedPath;
}

async function handleLifecycleAction() {
  if (isBusy.value || (!props.assignmentEntryId && !props.assignmentSeriesId)) {
    return;
  }

  if (!assignmentLifecycle.value.canDelete && !assignmentLifecycle.value.canCancel) {
    return;
  }

  isSaving.value = true;
  apiError.value = '';

  try {
    const result =
      isSeriesScope.value && props.assignmentSeriesId
        ? assignmentLifecycle.value.canCancel
          ? await expireAssignmentSeries(props.assignmentSeriesId)
          : await deleteAssignmentSeries(props.assignmentSeriesId)
        : props.assignmentEntryId
          ? assignmentLifecycle.value.canCancel
            ? await expireAssignmentEntry(props.assignmentEntryId)
            : await deleteAssignmentEntry(props.assignmentEntryId)
          : null;

    if (!result) {
      apiError.value = 'Could not determine the assignment to update.';
      return;
    }

    if (result.error.value) {
      apiError.value =
        result.error.value.message ||
        (assignmentLifecycle.value.canCancel ? 'Failed to cancel assignment.' : 'Failed to delete assignment.');
      return;
    }

    calendarStore.refresh();
    emit('close');
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'An unexpected error occurred.';
  } finally {
    isSaving.value = false;
  }
}

async function loadInitialAssignment() {
  if (modalMode.value === 'create') {
    return;
  }

  assignmentLoadState.value = 'loading';
  assignmentLoadError.value = '';
  assignmentStatusTypeCode.value = null;

  try {
    const result =
      isSeriesScope.value && props.assignmentSeriesId
        ? await loadAssignmentSeriesById(props.assignmentSeriesId)
        : props.assignmentEntryId
          ? await loadAssignmentEntry(props.assignmentEntryId)
          : null;

    if (!result) {
      assignmentLoadError.value = 'Could not determine the assignment to load.';
      assignmentLoadState.value = 'loadError';
      return;
    }

    if (result.error.value) {
      assignmentLoadError.value = result.error.value.message || 'Failed to load assignment.';
      assignmentLoadState.value = 'loadError';
      return;
    }

    if (result.data.value) {
      if (isSeriesScope.value) {
        const series = result.data.value as AssignmentSeriesResponse;
        assignmentStatusTypeCode.value = series.statusTypeCode ?? null;
        formData.value = createFormDataFromAssignmentSeries(series);
      } else {
        const entry = result.data.value as AssignmentEntryResponse;
        assignmentStatusTypeCode.value = entry.statusTypeCode ?? null;
        formData.value = createFormDataFromAssignmentEntry(entry);
      }
      applyScopeRestrictions();
      if (props.initialShiftEntryIds !== undefined) {
        hasAppliedInitialShiftEntrySelection.value = false;
      }
      await loadShiftOptions();
      assignmentLoadState.value = 'loaded';
    } else {
      assignmentLoadError.value = 'Failed to load assignment.';
      assignmentLoadState.value = 'loadError';
    }
  } catch (error: unknown) {
    assignmentLoadError.value = error instanceof Error ? error.message : 'Failed to load assignment.';
    assignmentLoadState.value = 'loadError';
  }
}

function applyScopeRestrictions() {
  if (modalMode.value === 'create') {
    return;
  }

  if (isSeriesScope.value) {
    updateField('repeatMode', 'custom');
    updateField('shiftEntryLinks', []);
    selectedShiftEntryId.value = null;
    return;
  }

  updateField('repeatMode', 'never');
  updateField('recurrenceRule', null);
  updateField('shiftSeriesLinks', []);
  selectedShiftSeriesId.value = null;
}

function createFormDataFromAssignmentEntry(entry: AssignmentEntryResponse): AssignmentFormData {
  const shiftEntryLinks = resolveShiftEntryLinksFromAssignmentEntry(entry);

  return {
    ...createAssignmentFormDataBase(entry),
    assignmentSeriesId: entry.assignmentSeriesId ?? null,
    seriesStartAtUtc: entry.seriesStartAtUtc ?? null,
    seriesEndAtUtc: entry.seriesEndAtUtc ?? null,
    shiftEntryLinks,
  };
}

function createFormDataFromAssignmentSeries(series: AssignmentSeriesResponse): AssignmentFormData {
  const shiftSeriesLinks = resolveShiftSeriesLinksFromAssignmentSeries(series);

  return {
    ...createAssignmentFormDataBase(series),
    repeatMode: 'custom',
    recurrenceRule: series.recurrenceRule ?? null,
    shiftSeriesLinks,
  };
}

function createAssignmentFormDataBase(
  assignment: AssignmentEntryResponse | AssignmentSeriesResponse,
): AssignmentFormData {
  const zone = assignment.timeZoneId || timeZoneId.value;
  const start = assignment.startAtUtc ? parseFormDateTime(assignment.startAtUtc, zone) : null;
  const end = assignment.endAtUtc ? parseFormDateTime(assignment.endAtUtc, zone) : null;

  return {
    ...createInitialAssignmentFormData(start?.date ?? props.initialDate),
    title: assignment.title ?? '',
    description: assignment.description ?? null,
    notes: assignment.notes ?? '',
    color: assignment.color?.trim() || defaultAssignmentColor,
    date: start?.date ?? props.initialDate ?? '',
    startTime: start?.time ?? defaultTime(formData.value.startTime),
    endTime: end?.time ?? defaultTime(formData.value.endTime),
    repeatMode: 'never',
    recurrenceRule: null,
    allDay: assignment.allDay ?? false,
    locationId: assignment.locationId ?? null,
    assignmentDefinitionId: assignment.assignmentDefinitionId,
    categoryId: assignment.categoryId,
    subCategoryId: assignment.subCategoryId,
    capacity: assignment.capacity ?? 1,
    shiftSeriesLinks: [],
    shiftEntryLinks: [],
  };
}

function defaultTime(value: string | undefined) {
  return normalizeTimeOptionValue(value) || '08:00';
}

function getInitialOpenScope(): AssignmentOpenScope | null {
  if (props.editScope) {
    return props.editScope;
  }

  if (props.assignmentSeriesId) {
    return props.assignmentEntryId ? null : 'series';
  }

  return 'event';
}
</script>

<template>
  <UaModal :title="modalTitle" width="840" :loading="isBusy" @close="handleClose">
    <template #alerts>
      <UaAlert v-if="assignmentLoadError" type="error">
        {{ assignmentLoadError }}
      </UaAlert>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
      <UaAlert v-if="assignmentLifecycle.status === 'published'" type="info">
        {{ publishedAssignmentMessage }}
      </UaAlert>
      <UaAlert v-if="assignmentLinkedToPublishedShift" type="info">
        {{ publishedShiftLinkMessage }}
      </UaAlert>
    </template>

    <template v-if="shouldShowDetailTabs" #secondary-header>
      <div class="assignment-modal__tabs" role="tablist" aria-label="Assignment Detail Tabs">
        <button
          v-for="tab in visibleAssignmentDetailTabs"
          :key="tab.id"
          :aria-selected="tab.id === activeTab"
          class="assignment-modal__tab"
          :class="{ 'assignment-modal__tab--active': tab.id === activeTab }"
          role="tab"
          type="button"
          @click="selectTab(tab.id)"
        >
          {{ tab.label }}
        </button>
      </div>
    </template>

    <div v-if="shouldShowOpenScopeChoice" class="assignment-modal__scope-choice">
      <p class="assignment-modal__scope-choice-text">This is one event in a series. What do you want to open?</p>
      <div class="assignment-modal__scope-choice-actions">
        <UaBtn variant="outlined" :disabled="isLoadingAssignment" @click="selectOpenScope('event')">
          Only this event
        </UaBtn>
        <UaBtn color="primary" variant="flat" :loading="isLoadingAssignment" @click="selectOpenScope('series')">
          The entire series
        </UaBtn>
      </div>
    </div>

    <CalendarSchedulingShiftDetailsPanel
      v-else-if="shouldShowReadOnlyDetails"
      aria-label="Assignment Details Panel"
      :detail-rows="assignmentDetailRows"
      :is-loading="isLoadingAssignment"
    />

    <UaFormGrid v-else-if="!isDeleteTab" label-width="150px">
      <label class="assignment-modal__label" for="assignment-modal-location">Location</label>
      <div class="assignment-modal__field">
        <UaSelect
          id="assignment-modal-location"
          :model-value="formData.locationId"
          :items="locationOptionsWithSelected"
          :error="Boolean(formErrors.locationId)"
          :disabled="fieldsDisabled"
          @update:model-value="updateLocation"
        />
        <p v-if="formErrors.locationId" class="assignment-modal__field-error">
          {{ formErrors.locationId }}
        </p>
      </div>

      <label class="assignment-modal__label" for="assignment-modal-assignment">Assignment Type</label>
      <div class="assignment-modal__field">
        <UaSelect
          id="assignment-modal-assignment"
          :model-value="formData.assignmentDefinitionId ?? null"
          :items="assignmentDefinitionOptions"
          :error="Boolean(formErrors.assignmentDefinitionId)"
          :disabled="fieldsDisabled || isLoadingAssignmentDefinitions"
          :loading="isLoadingAssignmentDefinitions"
          @update:model-value="updateAssignmentDefinition"
        />
        <p v-if="formErrors.assignmentDefinitionId" class="assignment-modal__field-error">
          {{ formErrors.assignmentDefinitionId }}
        </p>
      </div>

      <span aria-hidden="true"></span>
      <p class="assignment-modal__helper-text">
        Category, subcategory, capacity, and default times are inherited from the selected definition.
      </p>

      <UaTextField
        id="assignment-modal-capacity"
        label="Capacity"
        type="number"
        :model-value="String(formData.capacity ?? '')"
        :error-messages="formErrors.capacity"
        :disabled="fieldsDisabled"
        @update:model-value="(value: string) => updateField('capacity', Number(value))"
      />

      <UaTextField
        id="assignment-modal-date"
        label="Date"
        type="date"
        :model-value="formData.date"
        :error-messages="formErrors.date"
        :disabled="fieldsDisabled"
        @update:model-value="(value: string) => updateField('date', value)"
      />

      <span id="assignment-modal-time-label" class="assignment-modal__label">Time</span>
      <div class="assignment-modal__time-fields" aria-labelledby="assignment-modal-time-label">
        <div class="assignment-modal__field">
          <span class="assignment-modal__time-caption">Start</span>
          <UaSelect
            :model-value="formData.startTime"
            aria-label="Start Time"
            :items="timeOptions"
            :error="Boolean(formErrors.startTime)"
            :disabled="fieldsDisabled"
            @update:model-value="(value: SelectValue | undefined) => updateSelectField('startTime', value)"
          />
          <p v-if="formErrors.startTime" class="assignment-modal__field-error">
            {{ formErrors.startTime }}
          </p>
        </div>
        <div class="assignment-modal__field">
          <span class="assignment-modal__time-caption">End</span>
          <UaSelect
            :model-value="formData.endTime"
            aria-label="End Time"
            :items="timeOptions"
            :error="Boolean(formErrors.endTime)"
            :disabled="fieldsDisabled"
            @update:model-value="(value: SelectValue | undefined) => updateSelectField('endTime', value)"
          />
          <p v-if="formErrors.endTime" class="assignment-modal__field-error">
            {{ formErrors.endTime }}
          </p>
        </div>
      </div>

      <template v-if="shouldShowRecurrenceFields">
        <label class="assignment-modal__label" for="assignment-modal-repeat">Repeat</label>
        <div class="assignment-modal__field">
          <UaSelect
            id="assignment-modal-repeat"
            :model-value="formData.repeatMode"
            aria-label="Repeat"
            :items="repeatOptions"
            :error="Boolean(formErrors.repeatMode)"
            :disabled="fieldsDisabled || isSeriesScope"
            @update:model-value="(value: SelectValue | undefined) => updateSelectField('repeatMode', value)"
          />
          <p v-if="formErrors.repeatMode" class="assignment-modal__field-error">
            {{ formErrors.repeatMode }}
          </p>
        </div>

        <RRuleEditor
          v-if="formData.repeatMode === 'custom'"
          id-prefix="assignment-modal-recurrence"
          :model-value="formData.recurrenceRule ?? null"
          :start-date="formData.date ?? null"
          :disabled="fieldsDisabled"
          use-parent-grid
          @update:model-value="handleRecurrenceChange"
          @change="handleRecurrenceChange"
          @invalid="(reason: string) => (recurrenceError = reason)"
        />
        <template v-else>
          <span aria-hidden="true"></span>
          <p class="assignment-modal__helper-text">This assignment will not repeat.</p>
        </template>
      </template>

      <template v-if="formErrors.recurrenceRule">
        <span aria-hidden="true"></span>
        <p class="assignment-modal__field-error">
          {{ formErrors.recurrenceRule }}
        </p>
      </template>

      <label v-if="shouldShowRecurringShiftLinks" class="assignment-modal__label" for="assignment-modal-shift-series">
        Link Recurring Shift
      </label>
      <div v-if="shouldShowRecurringShiftLinks" class="assignment-modal__field">
        <v-select
          id="assignment-modal-shift-series"
          :model-value="selectedShiftSeriesId"
          :items="shiftSeriesOptions"
          item-title="description"
          item-value="code"
          hide-details="auto"
          :disabled="fieldsDisabled || isLoadingShiftOptions"
          :loading="isLoadingShiftOptions"
          @update:model-value="updateSelectedShiftSeries"
        >
          <template #item="{ props: itemProps, item }">
            <v-list-item v-bind="getCustomListItemProps(itemProps)">
              <v-list-item-title>{{ getShiftOptionDescription(item) }}</v-list-item-title>
              <v-list-item-subtitle v-if="getShiftOptionSubtitle(item)" class="assignment-modal__option-subtitle">
                {{ getShiftOptionSubtitle(item) }}
              </v-list-item-subtitle>
            </v-list-item>
          </template>
        </v-select>
        <p class="assignment-modal__helper-text">Shift series can only be linked when the assignment recurs.</p>
      </div>

      <template v-if="shouldShowRecurringShiftLinks">
        <template
          v-for="(link, index) in formData.shiftSeriesLinks ?? []"
          :key="`shift-series-link-${link.shiftSeriesId}`"
        >
          <span aria-hidden="true"></span>
          <section class="assignment-modal__link-section">
            <div class="assignment-modal__link-section-header">
              <h3 class="assignment-modal__link-section-title">Recurring Shift {{ index + 1 }}</h3>
              <UaBtn
                v-if="!isReadOnly"
                variant="text"
                :disabled="fieldsDisabled"
                :aria-label="`Remove Recurring Shift ${index + 1}`"
                @click="removeShiftSeriesLink(index)"
              >
                <v-icon :icon="mdiDelete" size="18" />
              </UaBtn>
            </div>
            <p class="assignment-modal__link-section-summary">{{ formatShiftSeriesLinkTitle(link.shiftSeriesId) }}</p>
            <p v-if="formatShiftSeriesLinkDetails(link.shiftSeriesId)" class="assignment-modal__link-section-details">
              {{ formatShiftSeriesLinkDetails(link.shiftSeriesId) }}
            </p>
            <v-select
              :model-value="link.assignedUserIds"
              :items="getShiftSeriesUserOptions(link.shiftSeriesId)"
              item-title="description"
              item-value="code"
              label="Users"
              multiple
              chips
              closable-chips
              hide-details="auto"
              :error="Boolean(getLinkUserError('shiftSeriesLinks', index))"
              :disabled="fieldsDisabled"
              @update:model-value="(value: SelectValue | undefined) => updateShiftSeriesLinkUsers(index, value)"
            />
            <p v-if="getLinkUserError('shiftSeriesLinks', index)" class="assignment-modal__field-error">
              At least one user is required.
            </p>
          </section>
        </template>
      </template>

      <label v-if="shouldShowSingleShiftLinks" class="assignment-modal__label" for="assignment-modal-shift-entries">
        Link Shift
      </label>
      <div v-if="shouldShowSingleShiftLinks" class="assignment-modal__field">
        <v-select
          id="assignment-modal-shift-entries"
          :model-value="selectedShiftEntryId"
          :items="shiftEntryOptions"
          item-title="description"
          item-value="code"
          hide-details="auto"
          :disabled="fieldsDisabled || isLoadingShiftOptions"
          :loading="isLoadingShiftOptions"
          @update:model-value="updateSelectedShiftEntry"
        >
          <template #item="{ props: itemProps, item }">
            <v-list-item v-bind="getCustomListItemProps(itemProps)">
              <v-list-item-title>{{ getShiftOptionDescription(item) }}</v-list-item-title>
              <v-list-item-subtitle v-if="getShiftOptionSubtitle(item)" class="assignment-modal__option-subtitle">
                {{ getShiftOptionSubtitle(item) }}
              </v-list-item-subtitle>
            </v-list-item>
          </template>
        </v-select>
      </div>

      <template v-if="shouldShowSingleShiftLinks">
        <template
          v-for="(link, index) in formData.shiftEntryLinks ?? []"
          :key="`shift-entry-link-${link.shiftEntryId}`"
        >
          <span aria-hidden="true"></span>
          <section class="assignment-modal__link-section">
            <div class="assignment-modal__link-section-header">
              <h3 class="assignment-modal__link-section-title">Shift {{ index + 1 }}</h3>
              <UaBtn
                v-if="!isReadOnly"
                variant="text"
                :disabled="fieldsDisabled"
                :aria-label="`Remove Shift ${index + 1}`"
                @click="removeShiftEntryLink(index)"
              >
                <v-icon :icon="mdiDelete" size="18" />
              </UaBtn>
            </div>
            <p class="assignment-modal__link-section-summary">Shift</p>
            <p v-if="formatShiftEntryLinkDetails(link.shiftEntryId)" class="assignment-modal__link-section-details">
              {{ formatShiftEntryLinkDetails(link.shiftEntryId) }}
            </p>
            <v-select
              :model-value="link.assignedUserIds"
              :items="getShiftEntryUserOptions(link.shiftEntryId)"
              item-title="description"
              item-value="code"
              label="Users"
              multiple
              chips
              closable-chips
              hide-details="auto"
              :error="Boolean(getLinkUserError('shiftEntryLinks', index))"
              :disabled="fieldsDisabled"
              @update:model-value="(value: SelectValue | undefined) => updateShiftEntryLinkUsers(index, value)"
            />
            <p v-if="getLinkUserError('shiftEntryLinks', index)" class="assignment-modal__field-error">
              At least one user is required.
            </p>
          </section>
        </template>
      </template>

      <UaTextarea
        id="assignment-modal-notes"
        label="Notes"
        :model-value="formData.notes ?? ''"
        :disabled="fieldsDisabled"
        :error-messages="formErrors.notes"
        rows="3"
        counter="200"
        @update:model-value="(value: string) => updateField('notes', value)"
      />
    </UaFormGrid>

    <section v-else class="assignment-modal__delete-panel" aria-label="Assignment Lifecycle Panel">
      <h3 class="assignment-modal__delete-heading">
        {{ assignmentLifecycle.canCancel ? 'Cancel' : 'Delete' }}
        {{ isSeriesScope ? 'assignment series' : 'assignment' }}
      </h3>
      <p class="assignment-modal__delete-text">
        {{
          assignmentLifecycle.canCancel
            ? 'Cancelling this published assignment will remove it from active scheduling views.'
            : 'Deleting this draft assignment is permanent and cannot be undone.'
        }}
      </p>
    </section>

    <template v-if="assignmentLoadState === 'loadError'" #actions>
      <UaBtn variant="outlined" :disabled="isBusy" @click="handleClose">Close</UaBtn>
      <UaBtn color="primary" variant="flat" :loading="isLoadingAssignment" @click="loadInitialAssignment">Retry</UaBtn>
    </template>
    <template v-else-if="!isReadOnly || isDeleteTab" #actions>
      <template v-if="isDeleteTab">
        <UaBtn variant="outlined" :disabled="isBusy" @click="handleClose">Close</UaBtn>
        <UaBtn color="error" variant="flat" :loading="isSaving" @click="handleLifecycleAction">
          {{ assignmentLifecycle.canCancel ? 'Cancel Assignment' : 'Delete' }}
        </UaBtn>
      </template>
      <template v-else>
        <UaBtn
          variant="outlined"
          :disabled="isBusy"
          @click="shouldShowDetailTabs ? selectTab('details') : handleClose()"
        >
          Cancel
        </UaBtn>
        <UaBtn color="primary" variant="flat" :loading="isSaving" @click="handleSave">Save</UaBtn>
      </template>
    </template>
  </UaModal>
</template>

<style scoped>
.assignment-modal__label {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
}

.assignment-modal__field,
.assignment-modal__time-field {
  display: grid;
  gap: var(--ua-spacing-xs);
}

.assignment-modal__tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-lg);
}

.assignment-modal__scope-choice {
  display: grid;
  gap: 20px;
  padding: 8px 0;
}

.assignment-modal__scope-choice-text {
  margin: 0;
}

.assignment-modal__scope-choice-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  justify-content: flex-end;
}

.assignment-modal__tab {
  background: transparent;
  border: 0;
  border-bottom: 2px solid transparent;
  color: var(--ua-text-primary);
  cursor: pointer;
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-semibold);
  padding: 0 0 var(--ua-spacing-xs);
}

.assignment-modal__tab--active {
  border-bottom-color: rgb(var(--v-theme-primary));
}

.assignment-modal__delete-panel {
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  display: grid;
  gap: var(--ua-spacing-sm);
  padding: var(--ua-spacing-lg);
}

.assignment-modal__delete-heading {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-bold);
  margin: 0;
}

.assignment-modal__delete-text {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
}

.assignment-modal__link-section {
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  display: grid;
  gap: var(--ua-spacing-sm);
  padding: var(--ua-spacing-md);
}

.assignment-modal__link-section-header {
  align-items: center;
  display: flex;
  justify-content: space-between;
  gap: var(--ua-spacing-sm);
}

.assignment-modal__link-section-title {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  margin: 0;
}

.assignment-modal__link-section-summary,
.assignment-modal__link-section-details {
  margin: 0;
}

.assignment-modal__link-section-details {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  white-space: pre-line;
}

.assignment-modal__time-fields {
  display: grid;
  gap: var(--ua-spacing-md);
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.assignment-modal__time-caption {
  color: var(--ua-text-secondary);
  display: block;
  font-size: var(--ua-font-size-sm);
}

.assignment-modal__helper-text {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
}

.assignment-modal__field-error {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-sm);
  margin: var(--ua-spacing-xs) 0 0;
}

.assignment-modal__option-subtitle {
  -webkit-line-clamp: unset;
  overflow: visible;
  text-overflow: clip;
  white-space: pre-line;
}

:deep(.assignment-modal__option-subtitle .v-list-item-subtitle) {
  -webkit-line-clamp: unset;
  overflow: visible;
  text-overflow: clip;
  white-space: pre-line;
}

@media (max-width: 640px) {
  .assignment-modal__time-fields {
    grid-template-columns: 1fr;
  }
}
</style>
