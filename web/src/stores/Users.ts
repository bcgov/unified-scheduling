import { computed, shallowRef } from 'vue';
import { defineStore } from 'pinia';
import type { User } from '@/api-access/generated/models';

export const useUsersStore = defineStore('users', () => {
  const entities = shallowRef<User[]>([]);

  const entitiesMap = computed(() => {
    const map: Record<string, User> = {};
    entities.value.forEach((user) => {
      map[user.id] = user;
    });
    return map;
  });

  return { entities, entitiesMap };
});
