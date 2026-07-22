import { getApiSchedulingAssignmentDefinitions } from '@/api-access/generated/assignment-definition/assignment-definition';
import type { AssignmentDefinitionResponse } from '@/api-access/generated/models/assignmentDefinitionResponse';
import type { SelectOption } from '@/types/select';
import { DateTime } from 'luxon';
import { computed, ref, type ComputedRef } from 'vue';
import { isAssignmentDefinitionSelectableForAssignmentDate } from './assignmentDefinitionDateHelpers';

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
    if (!options.activeLocationId.value) {
      assignmentDefinitions.value = [];
      return;
    }

    isLoadingAssignmentDefinitions.value = true;

    try {
      const { data, error, execute } = getApiSchedulingAssignmentDefinitions(
        { locationId: options.activeLocationId.value },
        { options: { immediate: false } },
      );
      await execute();

      if (error.value) {
        options.onError(error.value.message || 'Failed to load assignment definitions.');
        return;
      }

      assignmentDefinitions.value = data.value ?? [];
      options.onLoaded?.();
    } catch (error: unknown) {
      options.onError(error instanceof Error ? error.message : 'Failed to load assignment definitions.');
    } finally {
      isLoadingAssignmentDefinitions.value = false;
    }
  }

  function upsertAssignmentDefinition(assignmentDefinition: AssignmentDefinitionResponse) {
    const nextAssignmentDefinitions = [...assignmentDefinitions.value];
    const existingIndex = nextAssignmentDefinitions.findIndex((candidate) => candidate.id === assignmentDefinition.id);

    if (existingIndex >= 0) {
      nextAssignmentDefinitions.splice(existingIndex, 1, assignmentDefinition);
    } else {
      nextAssignmentDefinitions.push(assignmentDefinition);
    }

    nextAssignmentDefinitions.sort((left, right) => (left.name || '').localeCompare(right.name || ''));
    assignmentDefinitions.value = nextAssignmentDefinitions;
  }

  function matchesActiveLocation(locationId: unknown) {
    const selectedLocationId = options.activeLocationId.value;
    return Boolean(selectedLocationId) && normalizeLocationId(locationId) === selectedLocationId;
  }

  function isAssignmentDefinitionValidForContextDate(assignmentDefinition: AssignmentDefinitionResponse) {
    const contextDate = options.contextDate.value;
    if (!contextDate.isValid) {
      return true;
    }

    return isAssignmentDefinitionSelectableForAssignmentDate(assignmentDefinition, contextDate, options.timeZoneId.value);
  }

  return {
    assignmentDefinitions,
    assignmentDefinitionOptions,
    isLoadingAssignmentDefinitions,
    loadAssignmentDefinitions,
    matchesActiveLocation,
    selectedAssignmentDefinition,
    upsertAssignmentDefinition,
  };
}

function normalizeLocationId(value: unknown) {
  const parsedLocationId = Number(value);
  return Number.isInteger(parsedLocationId) && parsedLocationId > 0 ? parsedLocationId : null;
}
