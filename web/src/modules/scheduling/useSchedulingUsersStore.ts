import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import { getApiUsers } from '@/api-access/generated/users/users';
import type { UserResponse } from '@/api-access/generated/models/userResponse';

const userCacheTtlMs = 5 * 60 * 1000;
const allUsersCacheKey = 'all';

export const useSchedulingUsersStore = defineStore('scheduling-users', () => {
  const usersByLocation = ref(new Map<number, UserResponse[]>());
  const loadedAtByLocation = ref(new Map<number, number>());
  const allUsers = ref<UserResponse[]>([]);
  const allUsersLoadedAt = ref<number | null>(null);
  const inFlightByLocation = new Map<number, Promise<UserResponse[]>>();
  let allUsersInFlight: Promise<UserResponse[]> | null = null;

  const usersById = computed(() => {
    const entries = [
      ...allUsers.value,
      ...Array.from(usersByLocation.value.values()).flatMap((users) => users),
    ];
    return new Map(entries.map((user) => [user.id, user]));
  });

  async function ensureUsersForLocation(locationId: number | null | undefined) {
    if (!locationId) {
      return [];
    }

    const cached = usersByLocation.value.get(locationId);
    const loadedAt = loadedAtByLocation.value.get(locationId);
    if (cached && isFresh(loadedAt)) {
      return cached;
    }

    const existingRequest = inFlightByLocation.get(locationId);
    if (existingRequest) {
      return existingRequest;
    }

    const request = fetchUsers({ LocationId: locationId })
      .then((users) => {
        usersByLocation.value = new Map(usersByLocation.value).set(locationId, users);
        loadedAtByLocation.value = new Map(loadedAtByLocation.value).set(locationId, Date.now());
        return users;
      })
      .finally(() => {
        inFlightByLocation.delete(locationId);
      });

    inFlightByLocation.set(locationId, request);
    return request;
  }

  async function ensureAllUsers() {
    if (allUsers.value.length && isFresh(allUsersLoadedAt.value)) {
      return allUsers.value;
    }

    if (allUsersInFlight) {
      return allUsersInFlight;
    }

    allUsersInFlight = fetchUsers({})
      .then((users) => {
        allUsers.value = users;
        allUsersLoadedAt.value = Date.now();
        return users;
      })
      .finally(() => {
        allUsersInFlight = null;
      });

    return allUsersInFlight;
  }

  function getUsersForLocation(locationId: number | null | undefined) {
    return locationId ? (usersByLocation.value.get(locationId) ?? []) : [];
  }

  function getUserById(userId: string) {
    return usersById.value.get(userId);
  }

  function invalidateLocation(locationId: number) {
    const nextUsersByLocation = new Map(usersByLocation.value);
    const nextLoadedAtByLocation = new Map(loadedAtByLocation.value);
    nextUsersByLocation.delete(locationId);
    nextLoadedAtByLocation.delete(locationId);
    usersByLocation.value = nextUsersByLocation;
    loadedAtByLocation.value = nextLoadedAtByLocation;
    inFlightByLocation.delete(locationId);
  }

  function invalidateAll() {
    usersByLocation.value = new Map();
    loadedAtByLocation.value = new Map();
    allUsers.value = [];
    allUsersLoadedAt.value = null;
    inFlightByLocation.clear();
    allUsersInFlight = null;
  }

  return {
    allUsers,
    ensureAllUsers,
    ensureUsersForLocation,
    getUserById,
    getUsersForLocation,
    invalidateAll,
    invalidateLocation,
    usersByLocation,
  };
});

function isFresh(loadedAt?: number | null) {
  return Boolean(loadedAt && Date.now() - loadedAt < userCacheTtlMs);
}

async function fetchUsers(params: { LocationId?: number }) {
  const { data, error, execute } = getApiUsers(
    {
      IsEnabled: true,
      ...params,
    },
    {
      options: { immediate: false },
    },
  );

  await execute();

  if (error.value) {
    throw error.value;
  }

  return data.value ?? [];
}
