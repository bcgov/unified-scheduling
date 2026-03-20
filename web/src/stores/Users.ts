import { computed, shallowRef } from 'vue';
import { defineStore } from 'pinia';
import type { UserResponse } from '@/api-access/generated/models';

export const useUsersStore = defineStore('users', () => {
  const entities = shallowRef<UserResponse[]>([]);

  const entitiesMap = computed(() => {
    const map: Record<string, UserResponse> = {};
    entities.value.forEach((user) => {
      map[user.id] = user;
    });
    return map;
  });

  return { entities, entitiesMap };
});
