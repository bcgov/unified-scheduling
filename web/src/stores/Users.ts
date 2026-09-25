import { getApiUsers } from '@/api-access/generated/users/users';
import type { UserResponse } from '@/api-access/generated/models';
import { defineStore } from 'pinia';
import { computed, ref, shallowRef } from 'vue';

const userCacheTtlMs = 5 * 60 * 1000;

interface InFlightUserRequest {
  activeConsumers: number;
  controller: AbortController;
  promise: Promise<UserResponse[]>;
  settled: boolean;
}

export const useUsersStore = defineStore('users', () => {
  const allUsers = shallowRef<UserResponse[]>([]);
  const entities = allUsers;
  const usersByLocation = ref(new Map<number, UserResponse[]>());
  const loadedAtByLocation = ref(new Map<number, number>());
  const allUsersLoadedAt = ref<number | null>(null);
  const inFlightByLocation = new Map<number, InFlightUserRequest>();
  let allUsersInFlight: InFlightUserRequest | null = null;

  const usersById = computed(() => {
    const records = new Map<string, { user: UserResponse; loadedAt: number }>();
    const globalLoadedAt = allUsersLoadedAt.value;

    for (const [locationId, users] of usersByLocation.value) {
      const loadedAt = loadedAtByLocation.value.get(locationId) ?? 0;
      if (globalLoadedAt != null && loadedAt <= globalLoadedAt) {
        continue;
      }

      for (const user of users) {
        const existing = records.get(user.id);
        if (!existing || loadedAt > existing.loadedAt) {
          records.set(user.id, { user, loadedAt });
        }
      }
    }

    const loadedAt = globalLoadedAt ?? 0;
    for (const user of allUsers.value) {
      const existing = records.get(user.id);
      if (!existing || loadedAt >= existing.loadedAt) {
        records.set(user.id, { user, loadedAt });
      }
    }

    return new Map(Array.from(records, ([userId, record]) => [userId, record.user]));
  });

  const entitiesMap = computed<Record<string, UserResponse>>(() => {
    return Object.fromEntries(allUsers.value.map((user) => [user.id, user]));
  });

  function ensureUsersForLocation(locationId: number | null | undefined, signal?: AbortSignal) {
    if (!locationId) {
      return Promise.resolve([]);
    }

    const cached = usersByLocation.value.get(locationId);
    const loadedAt = loadedAtByLocation.value.get(locationId);
    if (cached && isFresh(loadedAt)) {
      return Promise.resolve(cached);
    }

    const existingRequest = inFlightByLocation.get(locationId);
    if (existingRequest) {
      return consumeRequest(existingRequest, signal);
    }

    const request = createRequest((requestSignal) => fetchUsers({ LocationId: locationId }, requestSignal));
    inFlightByLocation.set(locationId, request);

    request.promise.then(
      (users) => {
        if (inFlightByLocation.get(locationId) === request) {
          usersByLocation.value = new Map(usersByLocation.value).set(locationId, users);
          loadedAtByLocation.value = new Map(loadedAtByLocation.value).set(locationId, Date.now());
          inFlightByLocation.delete(locationId);
        }
      },
      () => {
        if (inFlightByLocation.get(locationId) === request) {
          inFlightByLocation.delete(locationId);
        }
      },
    );

    return consumeRequest(request, signal);
  }

  function ensureAllUsers(signal?: AbortSignal) {
    if (isFresh(allUsersLoadedAt.value)) {
      return Promise.resolve(allUsers.value);
    }

    if (allUsersInFlight) {
      return consumeRequest(allUsersInFlight, signal);
    }

    const request = createRequest((requestSignal) => fetchUsers({}, requestSignal));
    allUsersInFlight = request;

    request.promise.then(
      (users) => {
        if (allUsersInFlight === request) {
          allUsers.value = users;
          allUsersLoadedAt.value = Date.now();
          allUsersInFlight = null;
        }
      },
      () => {
        if (allUsersInFlight === request) {
          allUsersInFlight = null;
        }
      },
    );

    return consumeRequest(request, signal);
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
    entities,
    entitiesMap,
    ensureAllUsers,
    ensureUsersForLocation,
    getUserById,
    getUsersForLocation,
    invalidateAll,
    invalidateLocation,
  };
});

function isFresh(loadedAt?: number | null) {
  return Boolean(loadedAt && Date.now() - loadedAt < userCacheTtlMs);
}

function createRequest(load: (signal: AbortSignal) => Promise<UserResponse[]>): InFlightUserRequest {
  const controller = new AbortController();
  const request: InFlightUserRequest = {
    activeConsumers: 0,
    controller,
    promise: Promise.resolve([]),
    settled: false,
  };

  request.promise = load(controller.signal).finally(() => {
    request.settled = true;
  });

  return request;
}

function consumeRequest(request: InFlightUserRequest, signal?: AbortSignal): Promise<UserResponse[]> {
  if (signal?.aborted) {
    if (!request.settled && request.activeConsumers === 0) {
      request.controller.abort();
    }

    return Promise.reject(createAbortError());
  }

  request.activeConsumers += 1;

  return new Promise<UserResponse[]>((resolve, reject) => {
    let completed = false;

    const complete = (callback: () => void) => {
      if (completed) {
        return;
      }

      completed = true;
      signal?.removeEventListener('abort', handleAbort);
      request.activeConsumers -= 1;
      callback();
    };

    const handleAbort = () => {
      complete(() => {
        if (!request.settled && request.activeConsumers === 0) {
          request.controller.abort();
        }

        reject(createAbortError());
      });
    };

    signal?.addEventListener('abort', handleAbort, { once: true });
    request.promise.then(
      (users) => complete(() => resolve(users)),
      (error: unknown) => complete(() => reject(error)),
    );
  });
}

function createAbortError() {
  return new DOMException('User request was aborted.', 'AbortError');
}

async function fetchUsers(params: { LocationId?: number }, signal?: AbortSignal) {
  const { data, error, execute } = getApiUsers(
    {
      IsEnabled: true,
      ...params,
    },
    {
      fetchOptions: { signal },
      options: { immediate: false },
    },
  );

  await execute();

  if (error.value) {
    throw error.value;
  }

  return data.value ?? [];
}
