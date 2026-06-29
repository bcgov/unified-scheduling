import { computed, ref, shallowRef } from 'vue';
import { defineStore } from 'pinia';
import type { LocationDto } from '@/api-access/generated/models';
import { getApiLocationAll } from '@/api-access/generated/location/location';
import type { SelectCode, SelectOption } from '@/types/select';
import { mapToSelectOptions } from '@/utils/select';

export const useLocationsStore = defineStore('locations', () => {
  const entities = ref<LocationDto[]>([]);
  const selectedLocationId = shallowRef<SelectCode | null>(null);

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

  const selectOptions = computed<SelectOption[]>(() =>
    mapToSelectOptions(
      entities.value,
      (location) => location.id,
      (location) => location.name,
    ),
  );

  const setSelectedLocationId = (id: SelectCode | null) => {
    selectedLocationId.value = id;
  };

  return { entities, entitiesMap, selectedLocationId, setSelectedLocationId, getEntities, selectOptions };
});
