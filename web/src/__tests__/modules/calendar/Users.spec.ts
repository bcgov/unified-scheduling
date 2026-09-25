import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createTestApp } from '@/__tests__/helpers/createTestApp';

describe('useUsersStore', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('dedupes in-flight location user requests and returns cached users', async () => {
    const execute = vi.fn().mockResolvedValue(undefined);
    const getApiUsers = vi.fn().mockReturnValue({
      data: {
        value: [
          {
            id: 'user-1',
            firstName: 'Alex',
            lastName: 'Alpha',
          },
        ],
      },
      error: { value: null },
      execute,
    });

    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers,
    }));

    const [{ useUsersStore }, app] = await Promise.all([
      import('@/stores/Users'),
      createTestApp({ loadConfig: false }),
    ]);
    const store = useUsersStore(app.pinia);

    const [firstUsers, secondUsers] = await Promise.all([
      store.ensureUsersForLocation(12),
      store.ensureUsersForLocation(12),
    ]);
    const cachedUsers = await store.ensureUsersForLocation(12);

    expect(firstUsers).toEqual(secondUsers);
    expect(cachedUsers).toEqual(firstUsers);
    expect(getApiUsers).toHaveBeenCalledTimes(1);
    expect(getApiUsers).toHaveBeenCalledWith(
      {
        IsEnabled: true,
        LocationId: 12,
      },
      {
        fetchOptions: { signal: expect.any(AbortSignal) },
        options: { immediate: false },
      },
    );
    expect(execute).toHaveBeenCalledTimes(1);
  });

  it('returns no users and does not fetch when location is missing', async () => {
    const getApiUsers = vi.fn();

    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers,
    }));

    const [{ useUsersStore }, app] = await Promise.all([
      import('@/stores/Users'),
      createTestApp({ loadConfig: false }),
    ]);
    const store = useUsersStore(app.pinia);

    await expect(store.ensureUsersForLocation(null)).resolves.toEqual([]);
    expect(getApiUsers).not.toHaveBeenCalled();
  });

  it('uses the newest cached record when a user exists in both location and all-user results', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-09-17T10:00:00Z'));

    const locationUsers = [{ id: 'user-1', firstName: 'Stale location name' }];
    const allUsers = [{ id: 'user-1', firstName: 'Current global name' }];
    const getApiUsers = vi.fn().mockImplementation((params: { LocationId?: number }) => ({
      data: { value: params.LocationId ? locationUsers : allUsers },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));

    vi.doMock('@/api-access/generated/users/users', () => ({ getApiUsers }));
    const [{ useUsersStore }, app] = await Promise.all([
      import('@/stores/Users'),
      createTestApp({ loadConfig: false }),
    ]);
    const store = useUsersStore(app.pinia);

    await store.ensureUsersForLocation(12);
    vi.advanceTimersByTime(1);
    await store.ensureAllUsers();

    expect(store.getUserById('user-1')).toEqual(allUsers[0]);
  });

  it('uses a newer location record instead of an older all-user record', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-09-17T10:00:00Z'));

    const allUsers = [{ id: 'user-1', firstName: 'Stale global name' }];
    const locationUsers = [{ id: 'user-1', firstName: 'Current location name' }];
    const getApiUsers = vi.fn().mockImplementation((params: { LocationId?: number }) => ({
      data: { value: params.LocationId ? locationUsers : allUsers },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));

    vi.doMock('@/api-access/generated/users/users', () => ({ getApiUsers }));
    const [{ useUsersStore }, app] = await Promise.all([
      import('@/stores/Users'),
      createTestApp({ loadConfig: false }),
    ]);
    const store = useUsersStore(app.pinia);

    await store.ensureAllUsers();
    vi.advanceTimersByTime(1);
    await store.ensureUsersForLocation(12);

    expect(store.getUserById('user-1')).toEqual(locationUsers[0]);
  });

  it('removes a user from the combined lookup when a newer all-user result omits it', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-09-17T10:00:00Z'));

    const locationUsers = [{ id: 'user-1', firstName: 'Disabled user' }];
    const getApiUsers = vi.fn().mockImplementation((params: { LocationId?: number }) => ({
      data: { value: params.LocationId ? locationUsers : [] },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));

    vi.doMock('@/api-access/generated/users/users', () => ({ getApiUsers }));
    const [{ useUsersStore }, app] = await Promise.all([
      import('@/stores/Users'),
      createTestApp({ loadConfig: false }),
    ]);
    const store = useUsersStore(app.pinia);

    await store.ensureUsersForLocation(12);
    vi.advanceTimersByTime(1);
    await store.ensureAllUsers();

    expect(store.getUserById('user-1')).toBeUndefined();
  });

  it('does not let an invalidated location request overwrite or clear a newer request', async () => {
    const oldRequest = deferred<void>();
    const newRequest = deferred<void>();
    const oldUsers = [{ id: 'old-user', firstName: 'Old' }];
    const newUsers = [{ id: 'new-user', firstName: 'New' }];
    const getApiUsers = vi
      .fn()
      .mockReturnValueOnce({ data: { value: oldUsers }, error: { value: null }, execute: () => oldRequest.promise })
      .mockReturnValueOnce({ data: { value: newUsers }, error: { value: null }, execute: () => newRequest.promise });

    vi.doMock('@/api-access/generated/users/users', () => ({ getApiUsers }));
    const [{ useUsersStore }, app] = await Promise.all([
      import('@/stores/Users'),
      createTestApp({ loadConfig: false }),
    ]);
    const store = useUsersStore(app.pinia);

    const stale = store.ensureUsersForLocation(12);
    store.invalidateLocation(12);
    const current = store.ensureUsersForLocation(12);
    oldRequest.resolve();
    await stale;

    expect(store.getUsersForLocation(12)).toEqual([]);
    const coalesced = store.ensureUsersForLocation(12);
    expect(getApiUsers).toHaveBeenCalledTimes(2);

    newRequest.resolve();
    await expect(Promise.all([current, coalesced])).resolves.toEqual([newUsers, newUsers]);
    expect(store.getUsersForLocation(12)).toEqual(newUsers);
  });

  it('does not let an invalidated all-users request overwrite or clear a newer request', async () => {
    const oldRequest = deferred<void>();
    const newRequest = deferred<void>();
    const oldUsers = [{ id: 'old-user', firstName: 'Old' }];
    const newUsers = [{ id: 'new-user', firstName: 'New' }];
    const getApiUsers = vi
      .fn()
      .mockReturnValueOnce({ data: { value: oldUsers }, error: { value: null }, execute: () => oldRequest.promise })
      .mockReturnValueOnce({ data: { value: newUsers }, error: { value: null }, execute: () => newRequest.promise });

    vi.doMock('@/api-access/generated/users/users', () => ({ getApiUsers }));
    const [{ useUsersStore }, app] = await Promise.all([
      import('@/stores/Users'),
      createTestApp({ loadConfig: false }),
    ]);
    const store = useUsersStore(app.pinia);

    const stale = store.ensureAllUsers();
    store.invalidateAll();
    const current = store.ensureAllUsers();
    oldRequest.resolve();
    await stale;

    expect(store.allUsers).toEqual([]);
    const coalesced = store.ensureAllUsers();
    expect(getApiUsers).toHaveBeenCalledTimes(2);

    newRequest.resolve();
    await expect(Promise.all([current, coalesced])).resolves.toEqual([newUsers, newUsers]);
    expect(store.allUsers).toEqual(newUsers);
  });
});

function deferred<T>() {
  let resolve!: (value: T | PromiseLike<T>) => void;
  const promise = new Promise<T>((complete) => {
    resolve = complete;
  });
  return { promise, resolve };
}
