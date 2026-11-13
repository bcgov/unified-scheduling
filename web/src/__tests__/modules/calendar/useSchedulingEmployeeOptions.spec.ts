import { describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { effectScope, ref } from 'vue';
import { flushPromises } from '@vue/test-utils';
import { useUsersStore } from '@/stores/Users';
import { useSchedulingEmployeeOptions } from '@/modules/scheduling/useSchedulingEmployeeOptions';
import { createInitialShiftFormDataForCreateAction } from '@/modules/scheduling/calendarSchedulingShiftForm';

vi.mock('@/composables/useAccessControl', () => ({
  useAccessControl: () => ({ hasPermission: () => false }),
}));
vi.mock('@/api-access/generated/users/users', () => ({
  getApiUsers: (params: { LocationId?: number }) => ({
    data: {
      value: params.LocationId
        ? []
        : [
            {
              id: 'moved-user',
              firstName: 'Moved',
              lastName: 'Employee',
              homeLocationId: 15,
              userLocations: [{ locationId: 12, effectiveFrom: null, effectiveUntil: '2026-11-13T08:00:00Z' }],
            },
          ],
    },
    error: { value: null },
    execute: vi.fn().mockResolvedValue(undefined),
  }),
}));

describe('useSchedulingEmployeeOptions', () => {
  it('retains home employees when loading all users fails', async () => {
    setActivePinia(createPinia());
    const usersStore = useUsersStore();
    const homeUser = { id: 'home-user', firstName: 'Home', lastName: 'Employee', homeLocationId: 12 };
    vi.spyOn(usersStore, 'ensureUsersForLocation').mockResolvedValue([homeUser]);
    vi.spyOn(usersStore, 'ensureAllUsers').mockRejectedValue(new Error('Global lookup failed'));
    const onError = vi.fn();
    const scope = effectScope();
    try {
      const options = scope.run(() =>
        useSchedulingEmployeeOptions(
          ref(12),
          ref({ ...createInitialShiftFormDataForCreateAction(12, 30), date: '2026-11-12' }),
          { onError },
        ),
      )!;
      await flushPromises();
      expect(options.employeeOptions.value.map((option) => option.code)).toEqual(['home-user']);
      expect(onError).toHaveBeenCalledWith('Global lookup failed');
      expect(options.isLoadingUsers.value).toBe(false);
    } finally {
      scope.stop();
      vi.restoreAllMocks();
    }
  });

  it('updates moved employees when the shift date or location changes', async () => {
    setActivePinia(createPinia());
    const scope = effectScope();
    const locationId = ref<number | null>(12);
    const formData = ref({ ...createInitialShiftFormDataForCreateAction(12, 30), date: '2026-11-12' });
    const options = scope.run(() => useSchedulingEmployeeOptions(locationId, formData))!;
    try {
      await flushPromises();
      expect(options.employeeOptions.value).toEqual([
        expect.objectContaining({ code: 'moved-user', description: expect.stringContaining('Moved Employee') }),
      ]);
      expect(options.getHomeEmployeeOptions(formData.value.date).map((option) => option.code)).toEqual(['moved-user']);
      formData.value.date = '2026-11-13';
      await flushPromises();
      expect(options.employeeOptions.value).toEqual([]);
      expect(options.getHomeEmployeeOptions(formData.value.date)).toEqual([]);
      locationId.value = 15;
      await flushPromises();
      expect(options.employeeOptions.value.map((option) => option.code)).toEqual(['moved-user']);
    } finally {
      scope.stop();
    }
  });
});
