import { computed, ref, watch } from 'vue';
import { DateTime } from 'luxon';
import { useDebounceFn } from '@vueuse/core';
import type { AuditEntityFieldDto, AuditRecordResponseDto } from '@/api-access/generated/models';
import { Permissions } from '@/api-access/generated/models';
import {
  getApiAuditHistory,
  getApiAuditSchemaEntityTypes,
  getApiAuditSchemaEntityTypesEntityTypeFields,
} from '@/api-access/generated/audit/audit';
import { getApiUsers } from '@/api-access/generated/users/users';
import { useAccessControl } from '@/composables/useAccessControl';
import type { SelectOption } from '@/types/select';
import { DEFAULT_PAGE_SIZE } from '../constants';

function currentWeekRange() {
  const monday = DateTime.now().startOf('week');
  const sunday = monday.plus({ days: 6 });
  return { from: monday.toISODate(), to: sunday.toISODate() };
}

export function useAuditHistory() {
  const { hasPermission } = useAccessControl();
  const canViewAudit = computed(() => hasPermission(Permissions.AuditRead));

  // ── Schema (entity types & their auditable fields) ──────────────────────
  const entityTypes = ref<string[]>([]);
  const fields = ref<AuditEntityFieldDto[]>([]);
  const isLoadingEntityTypes = ref(false);
  const isLoadingFields = ref(false);

  const fieldLabelByName = computed(() => new Map(fields.value.map((f) => [f.name, f.label])));
  const entityTypeOptions = computed<SelectOption[]>(() => entityTypes.value.map((t) => ({ code: t, description: t })));
  const changedFieldOptions = computed<SelectOption[]>(() =>
    fields.value.map((f) => ({ code: f.name, description: f.label })),
  );

  // ── Filters ───────────────────────────────────────────────────────────
  const { from: defaultFrom, to: defaultTo } = currentWeekRange();
  const entityType = ref<string | null>(null);
  const entityPk = ref<string | null>(null);
  const changedFields = ref<string[]>([]);
  const actorUserId = ref<string | null>(null);
  const fetchedActorOptions = ref<SelectOption[]>([]);
  const selectedActorOption = ref<SelectOption | null>(null);
  const isLoadingActors = ref(false);
  const action = ref<string | null>(null);
  const fromDate = ref<string | null>(defaultFrom);
  const toDate = ref<string | null>(defaultTo);
  const sortDirection = ref<'asc' | 'desc'>('desc');

  // Remote search results can clear or exclude the currently selected actor (e.g. after selection
  // triggers a follow-up search), so keep it pinned in the list the autocomplete resolves labels from.
  const actorOptions = computed<SelectOption[]>(() => {
    if (!selectedActorOption.value) {
      return fetchedActorOptions.value;
    }
    if (fetchedActorOptions.value.some((option) => option.code === selectedActorOption.value?.code)) {
      return fetchedActorOptions.value;
    }
    return [selectedActorOption.value, ...fetchedActorOptions.value];
  });

  watch(actorUserId, (newActorUserId) => {
    if (!newActorUserId) {
      selectedActorOption.value = null;
      return;
    }
    const match = actorOptions.value.find((option) => option.code === newActorUserId);
    if (match) {
      selectedActorOption.value = match;
    }
  });

  const isDateRangeValid = computed(() => {
    if (!fromDate.value || !toDate.value) {
      return true;
    }
    return DateTime.fromISO(fromDate.value) <= DateTime.fromISO(toDate.value);
  });

  const canApply = computed(
    () => !!entityType.value && !!fromDate.value && !!toDate.value && isDateRangeValid.value,
  );

  // ── Pagination ────────────────────────────────────────────────────────
  const page = ref(1);
  const pageSize = ref(DEFAULT_PAGE_SIZE);
  const totalCount = ref(0);
  const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize.value)));

  // ── Results ───────────────────────────────────────────────────────────
  const records = ref<AuditRecordResponseDto[]>([]);
  const expanded = ref<number[]>([]);
  const isLoadingRecords = ref(false);
  const error = ref('');
  const hasSearched = ref(false);

  // Filter values applied by the last Search click, snapshotted so that pagination/sort actions
  // re-query with the criteria the user actually searched for, not any since-edited draft filters.
  type AppliedFilters = {
    entityType: string | null;
    entityPk: string | null;
    changedFields: string[];
    actorUserId: string | null;
    action: string | null;
    fromDate: string | null;
    toDate: string | null;
  };
  const appliedFilters = ref<AppliedFilters | null>(null);

  function captureFilters(): AppliedFilters {
    return {
      entityType: entityType.value,
      entityPk: entityPk.value,
      changedFields: [...changedFields.value],
      actorUserId: actorUserId.value,
      action: action.value,
      fromDate: fromDate.value,
      toDate: toDate.value,
    };
  }

  watch(entityType, async (newEntityType, oldEntityType) => {
    if (newEntityType === oldEntityType) {
      return;
    }

    changedFields.value = [];
    fields.value = [];

    if (!newEntityType) {
      return;
    }

    isLoadingFields.value = true;
    try {
      const res = await getApiAuditSchemaEntityTypesEntityTypeFields(newEntityType);
      if (res.error.value) {
        error.value = res.error.value.message ?? 'Failed to load fields.';
        return;
      }
      fields.value = res.data.value?.fields ?? [];
    } finally {
      isLoadingFields.value = false;
    }
  });

  async function loadEntityTypes() {
    isLoadingEntityTypes.value = true;
    error.value = '';
    try {
      const res = await getApiAuditSchemaEntityTypes();
      if (res.error.value) {
        error.value = res.error.value.message ?? 'Failed to load entity types.';
        return;
      }
      entityTypes.value = res.data.value?.entityTypes ?? [];
    } finally {
      isLoadingEntityTypes.value = false;
    }
  }

  const searchActors = useDebounceFn(async (search: string) => {
    if (!search.trim()) {
      fetchedActorOptions.value = [];
      return;
    }

    isLoadingActors.value = true;
    try {
      const res = await getApiUsers({ Search: search.trim() });
      if (res.error.value) {
        error.value = res.error.value.message ?? 'Failed to load actors.';
        fetchedActorOptions.value = [];
        return;
      }
      fetchedActorOptions.value = (res.data.value ?? []).map((user) => ({
        code: user.id,
        description: `${user.firstName} ${user.lastName}`,
      }));
    } finally {
      isLoadingActors.value = false;
    }
  }, 300);

  function buildQueryParams() {
    const filters = appliedFilters.value ?? captureFilters();
    return {
      EntityType: filters.entityType ?? undefined,
      EntityPK: filters.entityPk?.trim() || undefined,
      Action: filters.action || undefined,
      ChangedField: filters.changedFields.length > 0 ? filters.changedFields : undefined,
      ActorUserId: filters.actorUserId ?? undefined,
      From: filters.fromDate
        ? (DateTime.fromISO(filters.fromDate).startOf('day').toUTC().toISO() ?? undefined)
        : undefined,
      To: filters.toDate ? (DateTime.fromISO(filters.toDate).endOf('day').toUTC().toISO() ?? undefined) : undefined,
      Page: page.value,
      PageSize: pageSize.value,
      SortDirection: sortDirection.value,
    };
  }

  async function search() {
    if (!entityType.value) {
      return;
    }

    isLoadingRecords.value = true;
    error.value = '';
    hasSearched.value = true;
    try {
      const res = await getApiAuditHistory(buildQueryParams());
      if (res.error.value) {
        error.value = res.error.value.message ?? 'Failed to load audit history.';
        records.value = [];
        totalCount.value = 0;
        return;
      }
      const data = res.data.value;
      records.value = data?.data ?? [];
      totalCount.value = data?.totalCount ?? 0;
      expanded.value = [];
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load audit history.';
      records.value = [];
      totalCount.value = 0;
    } finally {
      isLoadingRecords.value = false;
    }
  }

  function applyFilters() {
    page.value = 1;
    appliedFilters.value = captureFilters();
    return search();
  }

  function clearFilters() {
    entityType.value = null;
    entityPk.value = null;
    changedFields.value = [];
    actorUserId.value = null;
    selectedActorOption.value = null;
    fetchedActorOptions.value = [];
    action.value = null;
    fromDate.value = defaultFrom;
    toDate.value = defaultTo;
    sortDirection.value = 'desc';
    page.value = 1;
    appliedFilters.value = null;
    records.value = [];
    totalCount.value = 0;
    hasSearched.value = false;
  }

  function goToPage(newPage: number) {
    if (newPage < 1 || newPage > totalPages.value) {
      return;
    }
    page.value = newPage;
    return search();
  }

  function updatePageSize(newPageSize: number) {
    if (newPageSize === pageSize.value) {
      return;
    }
    pageSize.value = newPageSize;
    page.value = 1;
    return search();
  }

  function toggleSortDirection() {
    sortDirection.value = sortDirection.value === 'desc' ? 'asc' : 'desc';
    page.value = 1;
    return search();
  }

  return {
    canViewAudit,
    entityTypeOptions,
    changedFieldOptions,
    fieldLabelByName,
    isLoadingEntityTypes,
    isLoadingFields,
    entityType,
    entityPk,
    changedFields,
    actorUserId,
    actorOptions,
    isLoadingActors,
    searchActors,
    action,
    fromDate,
    toDate,
    sortDirection,
    canApply,
    page,
    pageSize,
    totalCount,
    totalPages,
    records,
    expanded,
    isLoadingRecords,
    error,
    hasSearched,
    loadEntityTypes,
    applyFilters,
    clearFilters,
    goToPage,
    updatePageSize,
    toggleSortDirection,
  };
}
