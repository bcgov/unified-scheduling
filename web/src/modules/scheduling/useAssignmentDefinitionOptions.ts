import { getApiSchedulingAssignmentDefinitions } from '@/api-access/generated/assignment-definition/assignment-definition';
import type { AssignmentDefinitionResponse } from '@/api-access/generated/models/assignmentDefinitionResponse';
import type { SelectOption } from '@/types/select';
import { DateTime } from 'luxon';
import { computed, ref, type ComputedRef } from 'vue';
import { isAssignmentDefinitionSelectableForAssignmentDate } from './assignmentDefinitionDateHelpers';
import { createLatestRequestGuard } from './latestRequestGuard';
import { parsePositiveInteger } from './calendarSchedulingShiftIds';

export function useAssignmentDefinitionOptions(options: {
  activeLocationId: ComputedRef<number | null>;
  timeZoneId: ComputedRef<string>;
  contextDate: ComputedRef<DateTime>;
  selectedAssignmentDefinitionId: ComputedRef<number | undefined>;
  onError: (message: string) => void;
  onLoaded?: () => void;
}) {
  const assignmentDefinitions = ref<AssignmentDefinitionResponse[]>([]);
  const isLoadingAssignmentDefinitions = ref(false);
  const requestGuard = createLatestRequestGuard();

  const assignmentDefinitionOptions = computed<SelectOption[]>(() =>
    assignmentDefinitions.value
      .filter((assignmentDefinition) => matchesActiveLocation(assignmentDefinition.locationId))
      .filter((assignmentDefinition) => isAssignmentDefinitionValidForContextDate(assignmentDefinition))
      .filter((assignmentDefinition) => typeof assignmentDefinition.id === 'number')
      .map((assignmentDefinition) => ({
        code: assignmentDefinition.id as number,
        description: assignmentDefinition.name || 'Assignment Type',
      })),
  );

  const selectedAssignmentDefinition = computed(() =>
    assignmentDefinitions.value.find((candidate) => candidate.id === options.selectedAssignmentDefinitionId.value),
  );

  async function loadAssignmentDefinitions() {
    const requestId = requestGuard.begin();
    const locationId = options.activeLocationId.value;
    if (!locationId) {
      assignmentDefinitions.value = [];
      isLoadingAssignmentDefinitions.value = false;
      return;
    }

    isLoadingAssignmentDefinitions.value = true;

    try {
      const { data, error, execute } = getApiSchedulingAssignmentDefinitions(
        { locationId },
        { options: { immediate: false } },
      );
      await execute();

      if (!requestGuard.isCurrent(requestId)) {
        return;
      }

      if (error.value) {
        options.onError(error.value.message || 'Failed to load assignment definitions.');
        return;
      }

      assignmentDefinitions.value = data.value ?? [];
      options.onLoaded?.();
    } catch (error: unknown) {
      if (requestGuard.isCurrent(requestId)) {
        options.onError(error instanceof Error ? error.message : 'Failed to load assignment definitions.');
      }
    } finally {
      if (requestGuard.isCurrent(requestId)) {
        isLoadingAssignmentDefinitions.value = false;
      }
    }
  }

  function matchesActiveLocation(locationId: unknown) {
    const selectedLocationId = options.activeLocationId.value;
    return Boolean(selectedLocationId) && parsePositiveInteger(locationId) === selectedLocationId;
  }

  function isAssignmentDefinitionValidForContextDate(assignmentDefinition: AssignmentDefinitionResponse) {
    const contextDate = options.contextDate.value;
    if (!contextDate.isValid) {
      return true;
    }

    return isAssignmentDefinitionSelectableForAssignmentDate(
      assignmentDefinition,
      contextDate,
      options.timeZoneId.value,
    );
  }

  return {
    assignmentDefinitions,
    assignmentDefinitionOptions,
    isLoadingAssignmentDefinitions,
    loadAssignmentDefinitions,
    matchesActiveLocation,
    selectedAssignmentDefinition,
  };
}
