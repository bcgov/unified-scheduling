import { computed, shallowRef } from 'vue';
import { defineStore } from 'pinia';
import type { LocationDto } from '@/api-access/generated/models';
import { getApiLocationAll } from '@/api-access/generated/location/location';
import type { SelectOption } from '@/types/select';
import { mapToSelectOptions } from '@/utils/select';

export const useLocationsStore = defineStore('locations', () => {
  const entities = shallowRef<LocationDto[]>([]);

  const entitiesMap = computed(() => {
    const map: Record<string, LocationDto> = {};
    entities.value.forEach((e) => {
      map[e.id as number] = e;
    });
    return map;
  });

  const getEntities = async () => {
    const { data } = await getApiLocationAll();
    entities.value = data.value || [];
  };

  const getSelectOptions = (): SelectOption[] => {
    return mapToSelectOptions(
      entities.value,
      (location) => location.id,
      (location) => location.name,
    );
  };

  return { entities, entitiesMap, getEntities, getSelectOptions };
});
