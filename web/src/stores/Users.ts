import { computed, shallowRef } from 'vue';
import { defineStore } from 'pinia';
import type { UserResponse } from '@/api-access/generated/models';

export const useUsersStore = defineStore('users', () => {
  const entities = shallowRef<UserResponse[]>([]);

  const entitiesMap = computed(() => {
    const map: Record<string, UserResponse> = {};
    entities.value.forEach((e) => {
      map[e.id] = e;
    });
    return map;
  });

  return { entities, entitiesMap };
});
