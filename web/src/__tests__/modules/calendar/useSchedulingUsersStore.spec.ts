import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createTestApp } from '@/__tests__/helpers/createTestApp';

describe('useSchedulingUsersStore', () => {
  beforeEach(() => {
    vi.resetModules();
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

    const [{ useSchedulingUsersStore }, app] = await Promise.all([
      import('@/modules/scheduling/useSchedulingUsersStore'),
      createTestApp({ loadConfig: false }),
    ]);
    const store = useSchedulingUsersStore(app.pinia);

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
      { options: { immediate: false } },
    );
    expect(execute).toHaveBeenCalledTimes(1);
  });

  it('returns no users and does not fetch when location is missing', async () => {
    const getApiUsers = vi.fn();

    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers,
    }));

    const [{ useSchedulingUsersStore }, app] = await Promise.all([
      import('@/modules/scheduling/useSchedulingUsersStore'),
      createTestApp({ loadConfig: false }),
    ]);
    const store = useSchedulingUsersStore(app.pinia);

    await expect(store.ensureUsersForLocation(null)).resolves.toEqual([]);
    expect(getApiUsers).not.toHaveBeenCalled();
  });
});
