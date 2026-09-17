import { computed, ref } from 'vue';
import type { AssignmentEntryResponse, AssignmentSeriesResponse } from '@/api-access/generated/models';
import { loadAssignmentEntry, loadAssignmentSeriesById } from './calendarSchedulingAssignmentApi';
import { createLatestRequestGuard } from './latestRequestGuard';

export type AssignmentLoadState = 'idle' | 'loading' | 'loaded' | 'loadError';

export function useSchedulingAssignmentLoad() {
  const state = ref<AssignmentLoadState>('idle');
  const error = ref('');
  const statusTypeCode = ref<string | null>(null);
  const guard = createLatestRequestGuard();
  const isLoading = computed(() => state.value === 'loading');

  async function load(
    kind: 'entry' | 'series',
    id: number | undefined,
    onLoaded: (assignment: AssignmentEntryResponse | AssignmentSeriesResponse) => void,
  ) {
    const requestId = guard.begin();
    state.value = 'loading';
    error.value = '';
    statusTypeCode.value = null;

    if (!id) {
      error.value = 'Could not determine the assignment to load.';
      state.value = 'loadError';
      return;
    }

    try {
      const result = kind === 'series' ? await loadAssignmentSeriesById(id) : await loadAssignmentEntry(id);
      if (!guard.isCurrent(requestId)) {
        return;
      }
      if (result.error.value || !result.data.value) {
        error.value = result.error.value?.message || 'Failed to load assignment.';
        state.value = 'loadError';
        return;
      }

      statusTypeCode.value = result.data.value.statusTypeCode ?? null;
      onLoaded(result.data.value);
      state.value = 'loaded';
    } catch (loadError: unknown) {
      if (guard.isCurrent(requestId)) {
        error.value = loadError instanceof Error ? loadError.message : 'Failed to load assignment.';
        state.value = 'loadError';
      }
    }
  }

  return { error, isLoading, load, state, statusTypeCode };
}
