import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import { isEffectiveDateRangeActive } from '@/modules/scheduling/effectiveDateRangeStatus';
import { useLocationsStore } from '@/stores/LocationsStore';
import { DateTime } from 'luxon';

const hasPermission = vi.hoisted(() => vi.fn(() => true));
vi.mock('@/composables/useAccessControl', () => ({ useAccessControl: () => ({ hasPermission }) }));

function createAwaitGuardedFetchResult<T>(value: T, execute = vi.fn().mockResolvedValue(undefined)) {
  const result = {
    data: { value },
    error: { value: null },
    execute,
  };

  return new Proxy(result, {
    get(target, property, receiver) {
      if (property === 'then') {
        throw new Error('useFetch result should not be awaited before execute()');
      }

      return Reflect.get(target, property, receiver);
    },
  });
}

function createFetchResult<T>({
  value,
  error = null,
  execute = vi.fn().mockResolvedValue(undefined),
}: {
  value: T;
  error?: unknown;
  execute?: ReturnType<typeof vi.fn>;
}) {
  return {
    data: { value },
    error: { value: error },
    execute,
  };
}

function createValidationError(errors: Record<string, string[]>) {
  return {
    message: 'Validation failed.',
    data: {
      errors,
    },
  };
}

async function mountAssignmentCreateModalWithSaveError(
  error: unknown,
  options: { recurring?: boolean; savedId?: number } = {},
) {
  const saveExecute = vi.fn().mockResolvedValue(undefined);
  const response = options.savedId ? { id: options.savedId } : null;
  const postAssignmentEntry = vi
    .fn()
    .mockReturnValue(createFetchResult({ value: response, error, execute: saveExecute }));
  const postAssignmentSeries = vi
    .fn()
    .mockReturnValue(createFetchResult({ value: response, error, execute: saveExecute }));

  vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
    getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue(
      createFetchResult({
        value: [
          {
            id: 7,
            name: 'Court coverage',
            categoryId: 10,
            subCategoryId: 20,
            locationId: 12,
            defaultCapacity: 1,
            defaultStartTime: '09:00',
            defaultEndTime: '17:00',
            effectiveDateUtc: '2026-07-01T00:00:00Z',
            expiryDateUtc: null,
          },
        ],
      }),
    ),
    postApiSchedulingAssignmentDefinitions: vi.fn(),
  }));
  vi.doMock('@/api-access/generated/shift/shift', () => ({
    getApiSchedulingShiftsSeries: vi.fn().mockReturnValue(
      createFetchResult({
        value: [
          {
            id: 200,
            title: 'Blue recurring shift',
            startAtUtc: '2026-07-13T16:00:00Z',
            endAtUtc: '2026-07-14T00:00:00Z',
            timeZoneId: 'America/Vancouver',
            locationId: 12,
            statusTypeCode: 'Active',
            userIds: ['feaa2a73-6898-48ae-9c32-9633b1ec5538'],
            recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=5',
          },
        ],
      }),
    ),
    getApiSchedulingShiftsEntries: vi.fn().mockReturnValue(
      createFetchResult({
        value: [
          {
            id: 42,
            shiftSeriesId: 200,
            title: 'Blue shift',
            startAtUtc: '2026-07-13T16:00:00Z',
            endAtUtc: '2026-07-14T00:00:00Z',
            timeZoneId: 'America/Vancouver',
            locationId: 12,
            statusTypeCode: 'Active',
            userIds: ['feaa2a73-6898-48ae-9c32-9633b1ec5538'],
          },
        ],
      }),
    ),
  }));
  vi.doMock('@/api-access/generated/users/users', () => ({
    getApiUsers: vi.fn().mockReturnValue(
      createFetchResult({
        value: [
          {
            id: 'feaa2a73-6898-48ae-9c32-9633b1ec5538',
            firstName: 'Alex',
            lastName: 'Alpha',
          },
        ],
      }),
    ),
  }));
  vi.doMock('@/api-access/generated/assignment/assignment', () => ({
    getApiSchedulingAssignmentsEntriesId: vi.fn(),
    getApiSchedulingAssignmentsSeriesId: vi.fn(),
    postApiSchedulingAssignmentsEntries: postAssignmentEntry,
    postApiSchedulingAssignmentsSeries: postAssignmentSeries,
    putApiSchedulingAssignmentsEntriesId: vi.fn(),
    putApiSchedulingAssignmentsSeriesId: vi.fn(),
    postApiSchedulingAssignmentsEntriesIdExpire: vi.fn(),
    postApiSchedulingAssignmentsSeriesIdExpire: vi.fn(),
  }));
  const { default: CalendarSchedulingAssignmentModal } =
    await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

  const app = await createTestApp({ loadConfig: false });
  const locationsStore = useLocationsStore(app.pinia);
  locationsStore.setSelectedLocationId(12);

  const wrapper = mount(CalendarSchedulingAssignmentModal, {
    props: {
      initialDate: '2026-07-13',
      initialAssignmentDefinitionId: 7,
      initialShiftEntryIds: options.recurring ? [42] : undefined,
      timeZone: 'America/Vancouver',
    },
    global: { plugins: app.mountPlugins },
    attachTo: document.body,
  });

  await flushPromises();

  const vm = wrapper.vm as unknown as {
    formData: {
      repeatMode?: string;
      recurrenceRule?: string | null;
      shiftEntryLinks?: Array<{ shiftEntryId: number; assignedUserIds: string[] }>;
      shiftSeriesLinks?: Array<{ shiftSeriesId: number; assignedUserIds: string[] }>;
    };
    formErrors: Record<string, string>;
  };

  if (options.recurring) {
    expect(vm.formData.shiftEntryLinks).toEqual([
      {
        shiftEntryId: 42,
        assignedUserIds: ['feaa2a73-6898-48ae-9c32-9633b1ec5538'],
      },
    ]);
    vm.formData.repeatMode = 'custom';
    vm.formData.recurrenceRule = 'RRULE:FREQ=DAILY;COUNT=5';
    await flushPromises();
  } else {
    vm.formData.shiftEntryLinks = [
      {
        shiftEntryId: 42,
        assignedUserIds: ['feaa2a73-6898-48ae-9c32-9633b1ec5538'],
      },
    ];
  }

  const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
    button.textContent?.includes('Save'),
  );
  saveButton?.dispatchEvent(new Event('click', { bubbles: true }));
  await flushPromises();

  return {
    wrapper,
    vm,
    postAssignmentEntry,
    postAssignmentSeries,
  };
}

describe('CalendarSchedulingAssignmentModal', () => {
  beforeEach(() => {
    vi.resetModules();
    hasPermission.mockReturnValue(true);
  });

  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('promotes a dragged shift entry to its series in the assignment series create request', async () => {
    const { wrapper, vm, postAssignmentSeries } = await mountAssignmentCreateModalWithSaveError(null, {
      recurring: true,
      savedId: 90,
    });

    postAssignmentSeries.mockClear();
    expect(vm.formData.shiftSeriesLinks).toEqual([
      {
        shiftSeriesId: 200,
        assignedUserIds: ['feaa2a73-6898-48ae-9c32-9633b1ec5538'],
      },
    ]);
    await (wrapper.vm as unknown as { handleSave: () => Promise<void> }).handleSave();

    expect(postAssignmentSeries).toHaveBeenCalledWith(
      expect.objectContaining({
        shiftSeriesLinks: [
          {
            shiftSeriesId: 200,
            assignedUserIds: ['feaa2a73-6898-48ae-9c32-9633b1ec5538'],
          },
        ],
      }),
      expect.objectContaining({ options: { immediate: false } }),
    );
    wrapper.unmount();
  });

  it('creates an assignment definition in the secondary modal and selects it in the parent modal', async () => {
    const activeNow = DateTime.fromISO('2026-07-09T12:00:00Z', { setZone: true }) as DateTime<true>;
    const getAssignmentDefinitionsExecute = vi.fn().mockResolvedValue(undefined);
    const getAssignmentTypesExecute = vi.fn().mockResolvedValue(undefined);
    const getAssignmentCategoriesExecute = vi.fn().mockResolvedValue(undefined);
    const getAssignmentSubCategoriesExecute = vi.fn().mockResolvedValue(undefined);
    const postAssignmentDefinitionExecute = vi.fn().mockResolvedValue(undefined);
    const postAssignmentExecute = vi.fn().mockResolvedValue(undefined);

    const getAssignmentDefinitions = vi.fn().mockReturnValue({
      data: {
        value: [
          {
            id: 7,
            name: 'Court coverage',
            assignmentTypeId: 7,
            assignmentTypeCode: 'COURT',
            assignmentCategoryTypeId: 10,
            assignmentSubCategoryTypeId: 20,
            locationId: 12,
            capacity: 1,
            defaultStartTime: '09:00',
            defaultEndTime: '10:00',
            effectiveDate: '2026-07-01T00:00:00Z',
            expiryDate: null,
          },
          {
            id: 8,
            name: 'Future coverage',
            assignmentTypeId: 8,
            assignmentTypeCode: 'FUTURE',
            assignmentCategoryTypeId: 10,
            assignmentSubCategoryTypeId: 20,
            locationId: 12,
            capacity: 1,
            defaultStartTime: '09:00',
            defaultEndTime: '10:00',
            effectiveDateUtc: '2099-01-01T00:00:00Z',
            expiryDateUtc: null,
          },
          {
            id: 9,
            name: 'Expired coverage',
            assignmentTypeId: 9,
            assignmentTypeCode: 'EXPIRED',
            assignmentCategoryTypeId: 10,
            assignmentSubCategoryTypeId: 20,
            locationId: 12,
            capacity: 1,
            defaultStartTime: '09:00',
            defaultEndTime: '10:00',
            effectiveDateUtc: '2026-06-01T00:00:00Z',
            expiryDateUtc: '2026-06-30T00:00:00Z',
          },
          {
            id: 10,
            name: 'Other location coverage',
            assignmentTypeId: 10,
            assignmentTypeCode: 'OTHER',
            assignmentCategoryTypeId: 10,
            assignmentSubCategoryTypeId: 20,
            locationId: 13,
            capacity: 1,
            defaultStartTime: '09:00',
            defaultEndTime: '10:00',
            effectiveDateUtc: '2026-07-01T00:00:00Z',
            expiryDateUtc: null,
          },
        ],
      },
      error: { value: null },
      execute: getAssignmentDefinitionsExecute,
    });

    const getAssignmentTypes = vi.fn().mockReturnValue({
      data: {
        value: [
          {
            id: 7,
            code: 'COURT',
            description: 'Court coverage',
            effectiveDate: '2026-07-01T00:00:00Z',
            expiryDate: null,
          },
          {
            id: 8,
            code: 'FUTURE',
            description: 'Future type',
            effectiveDate: '2099-01-01T00:00:00Z',
            expiryDate: null,
          },
          {
            id: 9,
            code: 'EXPIRED',
            description: 'Expired type',
            effectiveDate: '2026-06-01T00:00:00Z',
            expiryDate: '2026-06-30T00:00:00Z',
          },
        ],
      },
      error: { value: null },
      execute: getAssignmentTypesExecute,
    });
    const getLookupCodeType = vi.fn((codeType: string) => {
      if (codeType === 'AssignmentCategoryTypes') {
        return {
          data: {
            value: [
              {
                code: 'CourtRoom',
                description: 'Court Room',
                effectiveDate: '2020-06-10T00:00:00Z',
                expiryDate: null,
                parentCodeTypeId: 10,
                childCodeTypeIds: [20, 21],
              },
              {
                code: 'EscortRun',
                description: 'Transport Assignment',
                effectiveDate: '2020-06-10T00:00:00Z',
                expiryDate: null,
                parentCodeTypeId: 11,
                childCodeTypeIds: [30],
              },
            ],
          },
          error: { value: null },
          execute: getAssignmentCategoriesExecute,
        };
      }

      return {
        data: {
          value: [
            {
              code: 'PROVINCIAL',
              description: 'Provincial',
              effectiveDate: '2020-06-10T00:00:00Z',
              expiryDate: null,
              parentCodeTypeId: 10,
              childCodeTypeId: 20,
            },
            {
              code: 'SUPREME',
              description: 'Supreme',
              effectiveDate: '2020-06-10T00:00:00Z',
              expiryDate: null,
              parentCodeTypeId: 10,
              childCodeTypeId: 21,
            },
            {
              code: 'IN_CUSTODY',
              description: 'In custody',
              effectiveDate: '2020-06-10T00:00:00Z',
              expiryDate: null,
              parentCodeTypeId: 11,
              childCodeTypeId: 30,
            },
          ],
        },
        error: { value: null },
        execute: getAssignmentSubCategoriesExecute,
      };
    });

    const postAssignmentDefinition = vi.fn().mockReturnValue({
      data: {
        value: {
          id: 19,
          name: 'Patrol support',
          assignmentTypeId: 7,
          assignmentTypeCode: 'COURT',
          categoryId: 10,
          subCategoryId: 20,
          locationId: 12,
          capacity: 3,
          defaultStartTime: '09:00',
          defaultEndTime: '10:00',
          effectiveDate: '2026-07-09T11:59:59Z',
          expiryDate: null,
        },
      },
      error: { value: null },
      execute: postAssignmentDefinitionExecute,
    });

    const postAssignmentEntry = vi
      .fn()
      .mockReturnValue(createAwaitGuardedFetchResult({ id: 901 }, postAssignmentExecute));

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: getAssignmentDefinitions,
      postApiSchedulingAssignmentDefinitions: postAssignmentDefinition,
    }));
    vi.doMock('@/api-access/generated/assignment-type/assignment-type', () => ({
      getApiSchedulingAssignmentTypes: getAssignmentTypes,
    }));
    vi.doMock('@/api-access/generated/lookup/lookup', () => ({
      getApiLookupCodeType: getLookupCodeType,
    }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      postApiSchedulingAssignmentsEntries: postAssignmentEntry,
      postApiSchedulingAssignmentsSeries: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    const calendarStore = useCalendarStore(app.pinia);
    locationsStore.entities = [{ id: 12, name: 'Headquarters' }] as never[];
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        initialDate: '2026-07-12',
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const initialVm = wrapper.vm as unknown as {
      assignmentDefinitionOptions: Array<{ code: number; description: string }>;
    };
    expect(initialVm.assignmentDefinitionOptions.map((option) => option.code)).toEqual([7]);

    const openAssignmentTypeButton = document.querySelector('button[aria-label="Add Assignment Type"]');
    openAssignmentTypeButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    const assignmentDefinitionModal = wrapper.findComponent({
      name: 'CalendarSchedulingAssignmentDefinitionCreateModal',
    });
    const assignmentDefinitionModalVm = assignmentDefinitionModal.vm as unknown as {
      formData: {
        code?: string;
        name?: string;
        description?: string;
        categoryId?: number;
        subCategoryId?: number;
        color?: string;
        defaultCapacity?: number;
        defaultStartTime?: string;
        defaultEndTime?: string;
        effectiveDateUtc?: string | null;
        locationId?: number | string;
      };
    };

    expect(assignmentDefinitionModalVm.formData.locationId).toBe(12);
    expect(assignmentDefinitionModalVm.formData.effectiveDateUtc).toBe('2026-07-12');

    assignmentDefinitionModalVm.formData.name = 'Patrol support';
    assignmentDefinitionModalVm.formData.description = '';
    assignmentDefinitionModalVm.formData.categoryId = 10;
    assignmentDefinitionModalVm.formData.subCategoryId = 20;
    assignmentDefinitionModalVm.formData.color = 'green';
    assignmentDefinitionModalVm.formData.defaultCapacity = 3;
    assignmentDefinitionModalVm.formData.defaultStartTime = '09:00';
    assignmentDefinitionModalVm.formData.defaultEndTime = '10:00';

    const saveButtons = Array.from(document.querySelectorAll('button')).filter((button) =>
      button.textContent?.includes('Save'),
    );
    saveButtons.at(-1)?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(postAssignmentDefinition).toHaveBeenCalledWith(
      expect.objectContaining({
        name: 'Patrol support',
        description: '',
        categoryId: 10,
        subCategoryId: 20,
        color: 'green',
        defaultCapacity: 3,
        effectiveDateUtc: expect.any(String),
        expiryDateUtc: null,
        locationId: 12,
      }),
      expect.objectContaining({ options: { immediate: false } }),
    );

    const vm = wrapper.vm as unknown as {
      formData: {
        assignmentDefinitionId?: number | null;
        categoryId?: number | null;
        subCategoryId?: number | null;
        date?: string;
        startTime?: string;
        endTime?: string;
      };
    };

    expect(vm.formData.assignmentDefinitionId).toBe(19);
    expect(calendarStore.refreshNonce).toBe(1);
    expect(
      [
        {
          id: 7,
          code: 'COURT',
          description: 'Court coverage',
          effectiveDate: '2026-07-01T00:00:00Z',
          expiryDate: null,
        },
        {
          id: 8,
          code: 'FUTURE',
          description: 'Future type',
          effectiveDate: '2099-01-01T00:00:00Z',
          expiryDate: null,
        },
        {
          id: 9,
          code: 'EXPIRED',
          description: 'Expired type',
          effectiveDate: '2026-06-01T00:00:00Z',
          expiryDate: '2026-06-30T00:00:00Z',
        },
        {
          id: 19,
          code: 'PATROL',
          description: 'Patrol support',
          effectiveDate: '2026-07-09T11:59:59Z',
          expiryDate: null,
        },
      ]
        .filter((item) => isEffectiveDateRangeActive(item, activeNow))
        .map((item) => item.id),
    ).toEqual([7, 19]);

    vm.formData.date = '2026-07-12';
    vm.formData.startTime = '09:00';
    vm.formData.endTime = '10:00';
    expect(vm.formData.categoryId).toBe(10);
    expect(vm.formData.subCategoryId).toBe(20);

    await (vm as unknown as { handleSave: () => Promise<void> }).handleSave();

    await flushPromises();

    expect(postAssignmentEntry).toHaveBeenCalledWith(
      expect.objectContaining({
        categoryId: 10,
        subCategoryId: 20,
        locationId: 12,
      }),
      expect.objectContaining({ options: { immediate: false } }),
    );
    expect(postAssignmentEntry.mock.calls[0]?.[0]).toHaveProperty('color', 'white');
    expect(calendarStore.refreshNonce).toBe(2);
    expect(wrapper.emitted('close')).toBeTruthy();

    wrapper.unmount();
  });

  it('treats assignment types as active only when effective and not expired', () => {
    const now = DateTime.fromISO('2026-07-09T12:00:00Z', { setZone: true }) as DateTime<true>;

    expect(
      isEffectiveDateRangeActive(
        {
          effectiveDate: '2026-07-09T11:59:59Z',
          expiryDate: null,
        },
        now,
      ),
    ).toBe(true);
    expect(
      isEffectiveDateRangeActive(
        {
          effectiveDate: '2026-07-09T12:00:01Z',
          expiryDate: null,
        },
        now,
      ),
    ).toBe(false);
    expect(
      isEffectiveDateRangeActive(
        {
          effectiveDate: '2026-07-01T00:00:00Z',
          expiryDate: '2026-07-09T12:00:00Z',
        },
        now,
      ),
    ).toBe(false);
  });

  it('populates Link Shift options from entries returned for the assignment date and location', async () => {
    const getAssignmentDefinitionsExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftSeriesExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftEntriesExecute = vi.fn().mockResolvedValue(undefined);
    const getUsersExecute = vi.fn().mockResolvedValue(undefined);

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 7,
              name: 'Court coverage',
              assignmentTypeId: 7,
              assignmentTypeCode: 'COURT',
              categoryId: 10,
              subCategoryId: 20,
              locationId: 12,
              capacity: 1,
              defaultStartTime: '09:00',
              defaultEndTime: '10:00',
              effectiveDate: '2026-07-01T00:00:00Z',
              expiryDate: null,
            },
          ],
        },
        error: { value: null },
        execute: getAssignmentDefinitionsExecute,
      }),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftsSeries: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getShiftSeriesExecute,
      }),
      getApiSchedulingShiftsEntries: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 42,
              eventId: 420,
              startAtUtc: '2026-07-12T16:00:00Z',
              endAtUtc: '2026-07-13T00:00:00Z',
              timeZoneId: 'America/Vancouver',
              locationId: 12,
              statusTypeCode: 'Active',
              userIds: ['00000000-0000-0000-0000-000000000002'],
            },
          ],
        },
        error: { value: null },
        execute: getShiftEntriesExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: '00000000-0000-0000-0000-000000000002',
              firstName: 'Alex',
              lastName: 'Alpha',
            },
          ],
        },
        error: { value: null },
        execute: getUsersExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      postApiSchedulingAssignmentsEntries: vi.fn(),
      postApiSchedulingAssignmentsSeries: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);
    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        initialDate: '2026-07-12',
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      shiftEntryOptions: Array<{ code: number; description: string }>;
      formData: { shiftEntryLinks?: Array<{ shiftEntryId: number }> };
    };

    expect(vm.formData.shiftEntryLinks).toEqual([]);
    expect(vm.shiftEntryOptions.map((option) => option.code)).toEqual([42]);
    expect(vm.shiftEntryOptions[0]?.description).toContain('Jul 12 2026');

    wrapper.unmount();
  });

  it('selects a dragged assignment definition and auto-populates the linked shift users', async () => {
    const getAssignmentDefinitionsExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftSeriesExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftEntriesExecute = vi.fn().mockResolvedValue(undefined);
    const getUsersExecute = vi.fn().mockResolvedValue(undefined);
    const postAssignmentExecute = vi.fn().mockResolvedValue(undefined);
    const postAssignmentEntry = vi
      .fn()
      .mockReturnValue(createFetchResult({ value: { id: 90 }, execute: postAssignmentExecute }));

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 7,
              name: 'Court coverage',
              categoryId: 10,
              subCategoryId: 20,
              locationId: 12,
              defaultCapacity: 1,
              defaultStartTime: '09:00:00',
              defaultEndTime: '10:00:00',
              effectiveDateUtc: '2026-07-01T00:00:00Z',
              expiryDateUtc: null,
            },
          ],
        },
        error: { value: null },
        execute: getAssignmentDefinitionsExecute,
      }),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftsSeries: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getShiftSeriesExecute,
      }),
      getApiSchedulingShiftsEntries: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 42,
              eventId: 420,
              startAtUtc: '2026-07-12T16:00:00Z',
              endAtUtc: '2026-07-13T00:00:00Z',
              timeZoneId: 'America/Vancouver',
              locationId: 12,
              statusTypeCode: 'Active',
              userIds: ['feaa2a73-6898-48ae-9c32-9633b1ec5538', 'a410cec2-5b36-4b7c-a788-448d64ab9510'],
            },
          ],
        },
        error: { value: null },
        execute: getShiftEntriesExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 'feaa2a73-6898-48ae-9c32-9633b1ec5538',
              firstName: 'Alex',
              lastName: 'Alpha',
            },
            {
              id: 'a410cec2-5b36-4b7c-a788-448d64ab9510',
              firstName: 'Blair',
              lastName: 'Beta',
            },
          ],
        },
        error: { value: null },
        execute: getUsersExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      postApiSchedulingAssignmentsEntries: postAssignmentEntry,
      postApiSchedulingAssignmentsSeries: vi.fn(),
    }));
    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);
    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        initialDate: '2026-07-12',
        initialAssignmentDefinitionId: 7,
        initialShiftEntryIds: [42],
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      assignmentDefinitionOptions: Array<{ code: number }>;
      formData: {
        assignmentDefinitionId?: number;
        shiftEntryLinks?: Array<{ shiftEntryId: number; assignedUserIds: string[] }>;
      };
      formErrors: Record<string, string>;
      validateForm: () => unknown;
    };

    expect(vm.assignmentDefinitionOptions.map((option) => option.code)).toContain(7);
    expect(vm.formData.assignmentDefinitionId).toBe(7);
    expect(vm.formData.shiftEntryLinks).toEqual([
      {
        shiftEntryId: 42,
        assignedUserIds: ['feaa2a73-6898-48ae-9c32-9633b1ec5538', 'a410cec2-5b36-4b7c-a788-448d64ab9510'],
      },
    ]);
    const validated = vm.validateForm();
    expect(vm.formErrors).toEqual({});
    expect(validated).not.toBeNull();

    await (vm as unknown as { handleSave: () => Promise<void> }).handleSave();

    expect(postAssignmentEntry).toHaveBeenCalledWith(
      expect.objectContaining({
        assignmentDefinitionId: 7,
        categoryId: 10,
        subCategoryId: 20,
        locationId: 12,
        color: 'white',
        shiftEntryLinks: [
          {
            shiftEntryId: 42,
            assignedUserIds: ['feaa2a73-6898-48ae-9c32-9633b1ec5538', 'a410cec2-5b36-4b7c-a788-448d64ab9510'],
          },
        ],
      }),
      expect.objectContaining({ options: { immediate: false } }),
    );
    expect(postAssignmentExecute).toHaveBeenCalled();
    wrapper.unmount();
  });

  it('shows a validation error when a dragged assignment definition is missing required inherited fields', async () => {
    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue(
        createFetchResult({
          value: [
            {
              id: 7,
              name: 'Court coverage',
              locationId: 12,
              defaultCapacity: 1,
              defaultStartTime: '09:00:00',
              defaultEndTime: '17:00:00',
              effectiveDateUtc: '2026-07-01T00:00:00Z',
              expiryDateUtc: null,
            },
          ],
        }),
      ),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftsSeries: vi.fn().mockReturnValue(createFetchResult({ value: [] })),
      getApiSchedulingShiftsEntries: vi.fn().mockReturnValue(createFetchResult({ value: [] })),
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers: vi.fn().mockReturnValue(createFetchResult({ value: [] })),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');
    const app = await createTestApp({ loadConfig: false });
    useLocationsStore(app.pinia).setSelectedLocationId(12);
    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        initialDate: '2026-07-12',
        initialAssignmentDefinitionId: 7,
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();
    await (wrapper.vm as unknown as { handleSave: () => Promise<void> }).handleSave();

    expect(document.body.textContent).toContain('Could not save the assignment. Check the highlighted fields.');
    wrapper.unmount();
  });

  it('loads only active-location users when creating an assignment', async () => {
    const getAssignmentDefinitionsExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftSeriesExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftEntriesExecute = vi.fn().mockResolvedValue(undefined);
    const getUsersExecute = vi.fn().mockResolvedValue(undefined);

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 7,
              name: 'Court coverage',
              assignmentTypeId: 7,
              assignmentTypeCode: 'COURT',
              categoryId: 10,
              subCategoryId: 20,
              locationId: 12,
              capacity: 1,
              defaultStartTime: '09:00',
              defaultEndTime: '10:00',
              effectiveDate: '2026-07-01T00:00:00Z',
              expiryDate: null,
            },
          ],
        },
        error: { value: null },
        execute: getAssignmentDefinitionsExecute,
      }),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftsSeries: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getShiftSeriesExecute,
      }),
      getApiSchedulingShiftsEntries: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 42,
              eventId: 420,
              startAtUtc: '2026-07-12T16:00:00Z',
              endAtUtc: '2026-07-13T00:00:00Z',
              timeZoneId: 'America/Vancouver',
              locationId: 12,
              statusTypeCode: 'Active',
              userIds: ['00000000-0000-0000-0000-000000000099'],
            },
          ],
        },
        error: { value: null },
        execute: getShiftEntriesExecute,
      }),
    }));
    const getApiUsers = vi.fn().mockImplementation((params: { LocationId?: number }) => ({
      data: {
        value:
          params.LocationId === 12
            ? [
                {
                  id: '00000000-0000-0000-0000-000000000002',
                  firstName: 'Local',
                  lastName: 'User',
                },
              ]
            : [
                {
                  id: '00000000-0000-0000-0000-000000000002',
                  firstName: 'Local',
                  lastName: 'User',
                },
                {
                  id: '00000000-0000-0000-0000-000000000099',
                  firstName: 'Chief',
                  lastName: 'Sheriff',
                },
              ],
      },
      error: { value: null },
      execute: getUsersExecute,
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({ getApiUsers }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      postApiSchedulingAssignmentsEntries: vi.fn(),
      postApiSchedulingAssignmentsSeries: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);
    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        initialDate: '2026-07-12',
        initialShiftEntryIds: [42],
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formatShiftEntryLinkDetails: (shiftEntryId: number) => string;
      getShiftEntryUserOptions: (shiftEntryId: number) => Array<{ code: string; description: string }>;
    };

    expect(getApiUsers).toHaveBeenCalledOnce();
    expect(getApiUsers).toHaveBeenCalledWith(
      { IsEnabled: true, LocationId: 12 },
      {
        fetchOptions: { signal: expect.any(AbortSignal) },
        options: { immediate: false },
      },
    );
    expect(vm.formatShiftEntryLinkDetails(42)).toContain('00000000-0000-0000-0000-000000000099');
    expect(vm.getShiftEntryUserOptions(42)).toEqual([
      {
        code: '00000000-0000-0000-0000-000000000099',
        description: '00000000-0000-0000-0000-000000000099',
      },
    ]);

    wrapper.unmount();
  });

  it('does not load users when there is no active location', async () => {
    const getAssignmentDefinitionsExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftSeriesExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftEntriesExecute = vi.fn().mockResolvedValue(undefined);
    const getUsersExecute = vi.fn().mockResolvedValue(undefined);
    const getApiUsers = vi.fn().mockReturnValue({
      data: { value: [] },
      error: { value: null },
      execute: getUsersExecute,
    });

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getAssignmentDefinitionsExecute,
      }),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftsSeries: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getShiftSeriesExecute,
      }),
      getApiSchedulingShiftsEntries: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getShiftEntriesExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers,
    }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      postApiSchedulingAssignmentsEntries: vi.fn(),
      postApiSchedulingAssignmentsSeries: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId('');
    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        initialDate: '2026-07-12',
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    expect(getApiUsers).not.toHaveBeenCalled();
    expect(getUsersExecute).not.toHaveBeenCalled();

    wrapper.unmount();
  });

  it('does not auto-link overlapping shifts when initial shift entries are explicitly empty', async () => {
    const getAssignmentDefinitionsExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftSeriesExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftEntriesExecute = vi.fn().mockResolvedValue(undefined);
    const getUsersExecute = vi.fn().mockResolvedValue(undefined);

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 7,
              name: 'Court coverage',
              assignmentTypeId: 7,
              assignmentTypeCode: 'COURT',
              assignmentCategoryTypeId: 10,
              assignmentSubCategoryTypeId: 20,
              locationId: 12,
              capacity: 1,
              defaultStartTime: '09:00',
              defaultEndTime: '10:00',
              effectiveDate: '2026-07-01T00:00:00Z',
              expiryDate: null,
            },
          ],
        },
        error: { value: null },
        execute: getAssignmentDefinitionsExecute,
      }),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftSeries: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getShiftSeriesExecute,
      }),
      getApiSchedulingShiftEntries: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 42,
              eventId: 420,
              startAtUtc: '2026-07-13T16:00:00Z',
              endAtUtc: '2026-07-14T00:00:00Z',
              timeZoneId: 'America/Vancouver',
              locationId: 12,
              statusTypeCode: 'Active',
              userIds: ['00000000-0000-0000-0000-000000000099'],
            },
          ],
        },
        error: { value: null },
        execute: getShiftEntriesExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getUsersExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      postApiSchedulingAssignmentsEntries: vi.fn(),
      postApiSchedulingAssignmentsSeries: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);
    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        initialDate: '2026-07-13',
        initialAssignmentDefinitionId: 7,
        initialShiftEntryIds: [],
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { shiftEntryLinks?: Array<{ shiftEntryId: number }>; shiftEntryIds?: number[] };
    };

    expect(vm.formData.shiftEntryIds).toEqual([]);
    expect(vm.formData.shiftEntryLinks).toEqual([]);

    wrapper.unmount();
  });

  it('auto-links only overlapping shifts from the assignment location', async () => {
    const getAssignmentDefinitionsExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftSeriesExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftEntriesExecute = vi.fn().mockResolvedValue(undefined);
    const getUsersExecute = vi.fn().mockResolvedValue(undefined);

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 7,
              name: 'OPS Assignment',
              assignmentTypeId: 7,
              assignmentTypeCode: 'OPS',
              assignmentCategoryTypeId: 10,
              assignmentSubCategoryTypeId: 20,
              locationId: 12,
              capacity: 1,
              defaultStartTime: '09:00',
              defaultEndTime: '17:00',
              effectiveDate: '2026-07-01T00:00:00Z',
              expiryDate: null,
            },
          ],
        },
        error: { value: null },
        execute: getAssignmentDefinitionsExecute,
      }),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftSeries: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getShiftSeriesExecute,
      }),
      getApiSchedulingShiftEntries: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 42,
              eventId: 420,
              startAtUtc: '2026-07-13T16:00:00Z',
              endAtUtc: '2026-07-14T00:00:00Z',
              timeZoneId: 'America/Vancouver',
              locationId: 12,
              statusTypeCode: 'Active',
              userIds: ['00000000-0000-0000-0000-000000000012'],
            },
            {
              id: 43,
              eventId: 430,
              startAtUtc: '2026-07-13T16:00:00Z',
              endAtUtc: '2026-07-14T00:00:00Z',
              timeZoneId: 'America/Vancouver',
              locationId: 99,
              statusTypeCode: 'Active',
              userIds: ['00000000-0000-0000-0000-000000000099'],
            },
          ],
        },
        error: { value: null },
        execute: getShiftEntriesExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getUsersExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      postApiSchedulingAssignmentsEntries: vi.fn(),
      postApiSchedulingAssignmentsSeries: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);
    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        initialDate: '2026-07-13',
        initialAssignmentDefinitionId: 7,
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { shiftEntryLinks?: Array<{ shiftEntryId: number }>; shiftEntryIds?: number[] };
    };

    expect(vm.formData.shiftEntryIds).toEqual([]);
    expect(vm.formData.shiftEntryLinks).toEqual([]);

    wrapper.unmount();
  });

  it('normalizes loaded assignment entry times for view and edit selects', async () => {
    const getAssignmentDefinitionsExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftSeriesExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftEntriesExecute = vi.fn().mockResolvedValue(undefined);
    const getUsersExecute = vi.fn().mockResolvedValue(undefined);
    const getAssignmentEntryExecute = vi.fn().mockResolvedValue(undefined);

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getAssignmentDefinitionsExecute,
      }),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftsSeries: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getShiftSeriesExecute,
      }),
      getApiSchedulingShiftsEntries: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 44,
              startAtUtc: '2026-07-13T16:00:00Z',
              endAtUtc: '2026-07-14T00:00:00Z',
              timeZoneId: 'America/Vancouver',
              locationId: 12,
              statusTypeCode: 'Draft',
              userIds: ['user-1'],
            },
          ],
        },
        error: { value: null },
        execute: getShiftEntriesExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getUsersExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      getApiSchedulingAssignmentsEntriesId: vi.fn().mockReturnValue(
        createAwaitGuardedFetchResult(
          {
            id: 257,
            assignmentSeriesId: 211,
            assignmentDefinitionId: 7,
            title: 'Court coverage',
            startAtUtc: '2026-07-13T16:00:00Z',
            endAtUtc: '2026-07-14T00:00:00Z',
            timeZoneId: 'America/Vancouver',
            locationId: 12,
            categoryId: 10,
            subCategoryId: 20,
            capacity: 1,
            linkedShiftEntryIds: [],
            assignedUserIds: [],
          },
          getAssignmentEntryExecute,
        ),
      ),
      getApiSchedulingAssignmentsSeriesId: vi.fn(),
      postApiSchedulingAssignmentsEntries: vi.fn(),
      postApiSchedulingAssignmentsSeries: vi.fn(),
      putApiSchedulingAssignmentsEntriesId: vi.fn(),
      putApiSchedulingAssignmentsSeriesId: vi.fn(),
      postApiSchedulingAssignmentsEntriesIdExpire: vi.fn(),
      postApiSchedulingAssignmentsSeriesIdExpire: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);
    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        mode: 'edit',
        assignmentEntryId: 257,
        initialShiftEntryIds: [44],
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: {
        startTime?: string;
        endTime?: string;
        shiftEntryLinks?: Array<{ shiftEntryId: number; assignedUserIds: string[] }>;
      };
    };

    expect(vm.formData.startTime).toBe('09:00');
    expect(vm.formData.endTime).toBe('17:00');
    expect(vm.formData.shiftEntryLinks).toEqual([{ shiftEntryId: 44, assignedUserIds: ['user-1'] }]);

    wrapper.unmount();
  });

  it('loads the entire series with only its shift staff available for the link', async () => {
    const getAssignmentDefinitionsExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftSeriesExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftEntriesExecute = vi.fn().mockResolvedValue(undefined);
    const getUsersExecute = vi.fn().mockResolvedValue(undefined);
    const getAssignmentSeriesExecute = vi.fn().mockResolvedValue(undefined);

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getAssignmentDefinitionsExecute,
      }),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftsSeries: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 200,
              title: 'Blue recurring shift',
              locationId: 12,
              statusTypeCode: 'Active',
              userIds: ['user-1'],
              startAtUtc: '2026-07-13T16:00:00Z',
              endAtUtc: '2026-07-14T00:00:00Z',
              timeZoneId: 'America/Vancouver',
              recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=5',
            },
          ],
        },
        error: { value: null },
        execute: getShiftSeriesExecute,
      }),
      getApiSchedulingShiftsEntries: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getShiftEntriesExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers: vi.fn().mockReturnValue({
        data: {
          value: [
            { id: 'user-1', firstName: 'Alex', lastName: 'Alpha' },
            { id: 'user-2', firstName: 'Blair', lastName: 'Beta' },
          ],
        },
        error: { value: null },
        execute: getUsersExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      getApiSchedulingAssignmentsEntriesId: vi.fn(),
      getApiSchedulingAssignmentsSeriesId: vi.fn().mockReturnValue(
        createAwaitGuardedFetchResult(
          {
            id: 211,
            assignmentDefinitionId: 7,
            title: 'Court coverage',
            startAtUtc: '2026-07-13T16:00:00Z',
            endAtUtc: '2026-07-14T00:00:00Z',
            timeZoneId: 'America/Vancouver',
            locationId: 12,
            assignmentCategoryTypeId: 10,
            assignmentSubCategoryTypeId: 20,
            capacity: 1,
            recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=5',
            entries: [],
            shiftSeriesLinks: [
              {
                id: 300,
                shiftSeriesId: 200,
                assignedUserIds: ['user-1'],
              },
            ],
          },
          getAssignmentSeriesExecute,
        ),
      ),
      postApiSchedulingAssignmentsEntries: vi.fn(),
      postApiSchedulingAssignmentsSeries: vi.fn(),
      putApiSchedulingAssignmentsEntriesId: vi.fn(),
      putApiSchedulingAssignmentsSeriesId: vi.fn(),
      postApiSchedulingAssignmentsEntriesIdExpire: vi.fn(),
      postApiSchedulingAssignmentsSeriesIdExpire: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    useLocationsStore(app.pinia).setSelectedLocationId(12);
    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        mode: 'view',
        assignmentEntryId: 257,
        assignmentSeriesId: 211,
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();
    expect(document.body.textContent).toContain('This is one event in a series. What do you want to open?');
    expect(getAssignmentSeriesExecute).not.toHaveBeenCalled();

    const seriesScopeButton = Array.from(document.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'The entire series',
    );
    seriesScopeButton?.dispatchEvent(new Event('click', { bubbles: true }));
    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { startTime?: string; endTime?: string };
      getShiftSeriesUserOptions: (shiftSeriesId: number) => Array<{ code: string; description: string }>;
    };

    expect(getAssignmentSeriesExecute).toHaveBeenCalledOnce();
    expect(vm.formData.startTime).toBe('09:00');
    expect(vm.formData.endTime).toBe('17:00');
    expect(vm.getShiftSeriesUserOptions(200)).toEqual([{ code: 'user-1', description: 'Alex Alpha' }]);

    wrapper.unmount();
  });

  it('renders assignment entry server validation errors on visible fields', async () => {
    const { wrapper, vm } = await mountAssignmentCreateModalWithSaveError(
      createValidationError({
        AssignmentDefinitionId: ['Required'],
        'ShiftEntryLinks[0].AssignedUserIds': ['Required'],
        StartAtUtc: ['Invalid start'],
        EndAtUtc: ['Invalid end'],
      }),
    );

    expect(vm.formErrors).toMatchObject({
      assignmentDefinitionId: 'Required',
      'shiftEntryLinks.0.assignedUserIds': 'Required',
      startTime: 'Invalid start',
      endTime: 'Invalid end',
    });
    expect(document.body.textContent).toContain('Required');
    expect(document.body.textContent).toContain('At least one user is required.');
    expect(document.body.textContent).toContain('Invalid start');
    expect(document.body.textContent).toContain('Invalid end');

    wrapper.unmount();
  });

  it('renders assignment series linked-shift user server validation errors on the linked row', async () => {
    const { wrapper, vm } = await mountAssignmentCreateModalWithSaveError(
      createValidationError({
        'ShiftSeriesLinks[0].AssignedUserIds': ['Required'],
      }),
      { recurring: true },
    );

    expect(vm.formErrors).toMatchObject({
      'shiftSeriesLinks.0.assignedUserIds': 'Required',
    });
    expect(document.body.textContent).toContain('At least one user is required.');

    wrapper.unmount();
  });

  it('renders generic assignment save errors as the api error banner', async () => {
    const { wrapper, vm } = await mountAssignmentCreateModalWithSaveError({
      message: 'Assignment save failed.',
    });

    expect(vm.formErrors).toEqual({});
    expect(document.body.textContent).toContain('Assignment save failed.');
    expect(wrapper.emitted('close')).toBeUndefined();

    wrapper.unmount();
  });

  it('does not load assignment definitions or shift options without an active location', async () => {
    const getApiSchedulingAssignmentDefinitions = vi.fn();
    const getApiSchedulingShiftSeries = vi.fn();
    const getApiSchedulingShiftEntries = vi.fn();

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions,
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftSeries,
      getApiSchedulingShiftEntries,
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      postApiSchedulingAssignmentsEntries: vi.fn(),
      postApiSchedulingAssignmentsSeries: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId('');

    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        initialDate: '2026-07-13',
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      assignmentDefinitionOptions: unknown[];
      shiftEntryOptions: unknown[];
      shiftSeriesOptions: unknown[];
    };

    expect(vm.assignmentDefinitionOptions).toEqual([]);
    expect(vm.shiftEntryOptions).toEqual([]);
    expect(vm.shiftSeriesOptions).toEqual([]);
    expect(getApiSchedulingAssignmentDefinitions).not.toHaveBeenCalled();
    expect(getApiSchedulingShiftEntries).not.toHaveBeenCalled();
    expect(getApiSchedulingShiftSeries).not.toHaveBeenCalled();

    wrapper.unmount();
  });

  it('filters assignment types by date and saves the selected definition', async () => {
    const postAssignmentEntry = vi.fn().mockReturnValue(createFetchResult({ value: { id: 91 } }));
    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue(
        createFetchResult({
          value: [
            {
              id: 7,
              name: 'July coverage',
              categoryId: 10,
              subCategoryId: 20,
              locationId: 12,
              defaultCapacity: 1,
              effectiveDateUtc: '2026-07-01T00:00:00Z',
              expiryDateUtc: '2026-08-01T00:00:00Z',
            },
            {
              id: 8,
              name: 'August coverage',
              categoryId: 10,
              subCategoryId: 20,
              locationId: 12,
              defaultCapacity: 1,
              effectiveDateUtc: '2026-08-01T00:00:00Z',
              expiryDateUtc: null,
            },
          ],
        }),
      ),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftsSeries: vi.fn().mockReturnValue(createFetchResult({ value: [] })),
      getApiSchedulingShiftsEntries: vi.fn().mockReturnValue(createFetchResult({ value: [] })),
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers: vi.fn().mockReturnValue(createFetchResult({ value: [] })),
    }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      postApiSchedulingAssignmentsEntries: postAssignmentEntry,
      postApiSchedulingAssignmentsSeries: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        initialDate: '2026-07-13',
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { date?: string; assignmentDefinitionId?: number };
      assignmentDefinitionOptions: Array<{ code: number; description: string }>;
      updateAssignmentDefinition: (value: number) => void;
      handleSave: () => Promise<void>;
    };

    expect(vm.assignmentDefinitionOptions.map((option) => option.code)).toEqual([7]);

    vm.updateAssignmentDefinition(7);
    await flushPromises();

    await vm.handleSave();
    expect(postAssignmentEntry).toHaveBeenCalledOnce();
    vm.formData.date = '2026-08-10';
    await flushPromises();

    expect(vm.assignmentDefinitionOptions.map((option) => option.code)).toEqual([8]);

    wrapper.unmount();
  });

  it('renders backend duplicate-assignment errors on the assignment-type field', async () => {
    const duplicateMessage =
      'An assignment already exists for location Headquarters, assignment definition Court coverage, and date 2026-07-13.';
    const { wrapper, vm } = await mountAssignmentCreateModalWithSaveError(
      createValidationError({ AssignmentDefinitionId: [duplicateMessage] }),
    );

    expect(vm.formErrors.assignmentDefinitionId).toBe(duplicateMessage);
    expect(document.body.textContent).toContain(duplicateMessage);
    expect(wrapper.emitted('close')).toBeUndefined();

    wrapper.unmount();
  });

  it('shows an error when a dropped assignment definition is not effective on the selected date', async () => {
    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue(
        createFetchResult({
          value: [
            {
              id: 7,
              name: 'Future coverage',
              assignmentCategoryTypeId: 10,
              assignmentSubCategoryTypeId: 20,
              locationId: 12,
              defaultCapacity: 1,
              effectiveDateUtc: '2026-08-01T07:00:00Z',
              expiryDateUtc: null,
            },
          ],
        }),
      ),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftsSeries: vi.fn().mockReturnValue(createFetchResult({ value: [] })),
      getApiSchedulingShiftsEntries: vi.fn().mockReturnValue(createFetchResult({ value: [] })),
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers: vi.fn().mockReturnValue(createFetchResult({ value: [] })),
    }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      postApiSchedulingAssignmentsEntries: vi.fn(),
      postApiSchedulingAssignmentsSeries: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        initialDate: '2026-07-13',
        initialAssignmentDefinitionId: 7,
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { assignmentDefinitionId?: number };
      assignmentDefinitionOptions: Array<{ code: number; description: string }>;
    };

    expect(vm.assignmentDefinitionOptions).toEqual([]);
    expect(vm.formData.assignmentDefinitionId).toBeUndefined();
    expect(document.body.textContent).toContain('Assignment Future coverage is not effective until August 1, 2026');

    wrapper.unmount();
  });

  it('filters assignment type options by the loaded assignment date in edit mode', async () => {
    const localUsers = [{ id: 'local-user', firstName: 'Local', lastName: 'User' }];
    const getApiUsers = vi
      .fn()
      .mockImplementation((params: { LocationId?: number }) =>
        params.LocationId
          ? createFetchResult({ value: localUsers })
          : createFetchResult({ value: [], error: new Error('All users unavailable.') }),
      );
    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue(
        createFetchResult({
          value: [
            {
              id: 7,
              name: 'July coverage',
              assignmentCategoryTypeId: 10,
              assignmentSubCategoryTypeId: 20,
              locationId: 12,
              defaultCapacity: 1,
              effectiveDateUtc: '2026-07-01T00:00:00Z',
              expiryDateUtc: '2026-08-01T00:00:00Z',
            },
            {
              id: 8,
              name: 'August coverage',
              assignmentCategoryTypeId: 10,
              assignmentSubCategoryTypeId: 20,
              locationId: 12,
              defaultCapacity: 1,
              effectiveDateUtc: '2026-08-01T00:00:00Z',
              expiryDateUtc: null,
            },
          ],
        }),
      ),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftsSeries: vi.fn().mockReturnValue(createFetchResult({ value: [] })),
      getApiSchedulingShiftsEntries: vi.fn().mockReturnValue(createFetchResult({ value: [] })),
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({ getApiUsers }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      getApiSchedulingAssignmentsEntriesId: vi.fn().mockReturnValue(
        createFetchResult({
          value: {
            id: 257,
            assignmentDefinitionId: 8,
            title: 'August assignment',
            startAtUtc: '2026-08-10T16:00:00Z',
            endAtUtc: '2026-08-11T00:00:00Z',
            timeZoneId: 'America/Vancouver',
            locationId: 12,
            assignmentCategoryTypeId: 10,
            assignmentSubCategoryTypeId: 20,
            capacity: 1,
            linkedShiftEntryIds: [],
            assignedUserIds: [],
          },
        }),
      ),
      getApiSchedulingAssignmentsSeriesId: vi.fn(),
      postApiSchedulingAssignmentsEntries: vi.fn(),
      postApiSchedulingAssignmentsSeries: vi.fn(),
      putApiSchedulingAssignmentsEntriesId: vi.fn(),
      putApiSchedulingAssignmentsSeriesId: vi.fn(),
      postApiSchedulingAssignmentsEntriesIdExpire: vi.fn(),
      postApiSchedulingAssignmentsSeriesIdExpire: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        mode: 'edit',
        assignmentEntryId: 257,
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { date?: string };
      assignmentDefinitionOptions: Array<{ code: number; description: string }>;
      users: Array<{ id: string }>;
      activeTab: string;
      isReadOnly: boolean;
    };

    expect(vm.formData.date).toBe('2026-08-10');
    expect(vm.assignmentDefinitionOptions.map((option) => option.code)).toEqual([8]);
    expect(vm.users).toEqual(localUsers);
    expect(getApiUsers).toHaveBeenCalledTimes(2);
    expect(document.body.textContent).not.toContain('All users unavailable.');
    expect(vm.activeTab).toBe('edit');
    expect(vm.isReadOnly).toBe(false);

    wrapper.unmount();
  });

  it('offers same-day shifts for other users at the assignment location when editing', async () => {
    const linkedUserId = '868d8b04-13ff-4b25-bd36-87c90a0d032d';
    const availableUserId = 'feaa2a73-6898-48ae-9c32-9633b1ec5538';
    const putAssignmentEntry = vi.fn().mockReturnValue(
      createFetchResult({
        value: { id: 257 },
      }),
    );

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue(
        createFetchResult({
          value: [
            {
              id: 7,
              name: 'Court Room Monitor',
              locationId: 12,
              categoryId: 6,
              subCategoryId: 25,
              color: 'pink',
              defaultCapacity: 1,
              effectiveDateUtc: '2026-08-01T00:00:00Z',
              expiryDateUtc: null,
            },
          ],
        }),
      ),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftsSeries: vi.fn().mockReturnValue(createFetchResult({ value: [] })),
      getApiSchedulingShiftsEntries: vi.fn().mockReturnValue(
        createFetchResult({
          value: [
            {
              id: 42,
              title: 'Linked shift',
              startAtUtc: '2026-08-25T16:00:00Z',
              endAtUtc: '2026-08-26T00:00:00Z',
              timeZoneId: 'America/Vancouver',
              locationId: 12,
              statusTypeCode: 'Draft',
              userIds: [linkedUserId],
            },
            {
              id: 43,
              title: 'Available shift',
              startAtUtc: '2026-08-25T16:00:00Z',
              endAtUtc: '2026-08-26T00:00:00Z',
              timeZoneId: 'America/Vancouver',
              locationId: 12,
              statusTypeCode: 'Draft',
              userIds: [availableUserId],
            },
          ],
        }),
      ),
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers: vi.fn().mockReturnValue(
        createFetchResult({
          value: [
            { id: linkedUserId, firstName: 'Mary', lastName: 'Park' },
            { id: availableUserId, firstName: 'Alex', lastName: 'Alpha' },
          ],
        }),
      ),
    }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      getApiSchedulingAssignmentsEntriesId: vi.fn().mockReturnValue(
        createFetchResult({
          value: {
            id: 257,
            assignmentDefinitionId: 7,
            title: 'Court Room Monitor',
            color: 'pink',
            startAtUtc: '2026-08-25T16:00:00Z',
            endAtUtc: '2026-08-26T00:00:00Z',
            timeZoneId: 'America/Vancouver',
            locationId: 12,
            categoryId: 6,
            subCategoryId: 25,
            capacity: 1,
            assignmentLinks: [
              {
                id: 90,
                shiftEntryId: 42,
                assignedUserIds: [linkedUserId],
              },
            ],
          },
        }),
      ),
      getApiSchedulingAssignmentsSeriesId: vi.fn(),
      putApiSchedulingAssignmentsEntriesId: putAssignmentEntry,
      putApiSchedulingAssignmentsSeriesId: vi.fn(),
      postApiSchedulingAssignmentsEntriesIdExpire: vi.fn(),
      postApiSchedulingAssignmentsSeriesIdExpire: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');
    const app = await createTestApp({ loadConfig: false });
    useLocationsStore(app.pinia).setSelectedLocationId(12);
    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        mode: 'edit',
        assignmentEntryId: 257,
        initialDate: '2026-08-25',
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();
    const vm = wrapper.vm as unknown as {
      formData: { shiftEntryLinks?: Array<{ shiftEntryId: number }> };
      shiftEntryOptions: Array<{ code: number; description: string }>;
      handleSave: () => Promise<void>;
    };

    expect(vm.formData.shiftEntryLinks?.map((link) => link.shiftEntryId)).toEqual([42]);
    expect(vm.shiftEntryOptions.map((option) => option.code)).toEqual([43]);
    expect(vm.shiftEntryOptions[0]?.description).toContain('Alex Alpha');

    await vm.handleSave();

    expect(putAssignmentEntry).toHaveBeenCalledWith(
      257,
      expect.not.objectContaining({ shiftEntryLinks: expect.anything() }),
      expect.objectContaining({ options: { immediate: false } }),
    );

    wrapper.unmount();
  });

  it('retries an entry edit with the acknowledged conflict in the same mutation', async () => {
    const resourceId = 'feaa2a73-6898-48ae-9c32-9633b1ec5538';
    const conflict = {
      id: '41:52:feaa2a73-6898-48ae-9c32-9633b1ec5538',
      entry: {
        eventId: 41,
        sourceModule: 'scheduling',
        title: 'Court Room Monitor',
        start: '2026-08-25T16:00:00Z',
        end: '2026-08-25T18:00:00Z',
        sourceEntityId: 257,
        timeZoneId: 'America/Vancouver',
      },
      overlaps: {
        eventId: 52,
        sourceModule: 'scheduling',
        title: 'Intake coverage',
        start: '2026-08-25T17:00:00Z',
        end: '2026-08-25T19:00:00Z',
        sourceEntityId: 258,
        timeZoneId: 'America/Vancouver',
      },
      resourceId,
      overlapStart: '2026-08-25T17:00:00Z',
      overlapEnd: '2026-08-25T18:00:00Z',
      isOverridden: false,
      overrideNote: null,
      createdById: null,
      createdOn: null,
      updatedById: null,
      updatedOn: null,
    };
    const putAssignmentEntry = vi
      .fn()
      .mockReturnValueOnce(
        createFetchResult({
          value: { message: 'The proposed change creates a calendar conflict.', conflicts: [conflict] },
          error: new Error('The proposed change creates a calendar conflict.'),
        }),
      )
      .mockReturnValueOnce(createFetchResult({ value: { id: 257 } }));

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue(
        createFetchResult({
          value: [
            {
              id: 7,
              name: 'Court Room Monitor',
              locationId: 12,
              categoryId: 6,
              subCategoryId: 25,
              color: 'pink',
              defaultCapacity: 1,
              effectiveDateUtc: '2026-08-01T00:00:00Z',
              expiryDateUtc: null,
            },
          ],
        }),
      ),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftsSeries: vi.fn().mockReturnValue(createFetchResult({ value: [] })),
      getApiSchedulingShiftsEntries: vi.fn().mockReturnValue(createFetchResult({ value: [] })),
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers: vi.fn().mockReturnValue(createFetchResult({ value: [] })),
    }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      getApiSchedulingAssignmentsEntriesId: vi.fn().mockReturnValue(
        createFetchResult({
          value: {
            id: 257,
            assignmentDefinitionId: 7,
            title: 'Court Room Monitor',
            color: 'pink',
            startAtUtc: '2026-08-25T16:00:00Z',
            endAtUtc: '2026-08-25T18:00:00Z',
            timeZoneId: 'America/Vancouver',
            locationId: 12,
            categoryId: 6,
            subCategoryId: 25,
            capacity: 1,
            assignmentLinks: [],
          },
        }),
      ),
      getApiSchedulingAssignmentsSeriesId: vi.fn(),
      putApiSchedulingAssignmentsEntriesId: putAssignmentEntry,
      putApiSchedulingAssignmentsSeriesId: vi.fn(),
      postApiSchedulingAssignmentsEntriesIdExpire: vi.fn(),
      postApiSchedulingAssignmentsSeriesIdExpire: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');
    const app = await createTestApp({ loadConfig: false });
    useLocationsStore(app.pinia).setSelectedLocationId(12);
    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        mode: 'edit',
        assignmentEntryId: 257,
        initialDate: '2026-08-25',
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();

    const vm = wrapper.vm as unknown as {
      handleSave: () => Promise<void>;
      handleConflictOverride: (note: string) => Promise<void>;
      pendingConflict: typeof conflict | null;
    };
    await vm.handleSave();
    expect(vm.pendingConflict).toEqual(conflict);

    await vm.handleConflictOverride('Operationally approved');

    expect(putAssignmentEntry).toHaveBeenCalledTimes(2);
    expect(putAssignmentEntry.mock.calls[1]?.[1]).toEqual(
      expect.objectContaining({
        conflictOverrides: [
          {
            firstEventId: 41,
            secondEventId: 52,
            resourceId,
            note: 'Operationally approved',
          },
        ],
      }),
    );
    expect(wrapper.emitted('close')).toHaveLength(1);
    wrapper.unmount();
  });

  it('loads shift options with the active location and assignment calendar day', async () => {
    const getApiSchedulingAssignmentDefinitions = vi.fn().mockReturnValue(
      createFetchResult({
        value: [
          {
            id: 7,
            name: 'Court coverage',
            assignmentCategoryTypeId: 10,
            assignmentSubCategoryTypeId: 20,
            locationId: 12,
            defaultCapacity: 1,
            effectiveDateUtc: '2026-07-01T00:00:00Z',
            expiryDateUtc: null,
          },
          {
            id: 8,
            name: 'Other location coverage',
            assignmentCategoryTypeId: 10,
            assignmentSubCategoryTypeId: 20,
            locationId: 13,
            defaultCapacity: 1,
            effectiveDateUtc: '2026-07-01T00:00:00Z',
            expiryDateUtc: null,
          },
        ],
      }),
    );
    const getApiSchedulingShiftsSeries = vi.fn().mockReturnValue(
      createFetchResult({
        value: [
          {
            id: 200,
            title: 'Location shift series',
            startAtUtc: '2026-07-13T16:00:00Z',
            endAtUtc: '2026-07-14T00:00:00Z',
            locationId: 12,
            statusTypeCode: 'Active',
            userIds: [],
          },
        ],
      }),
    );
    const getApiSchedulingShiftsEntries = vi.fn().mockReturnValue(
      createFetchResult({
        value: [
          {
            id: 42,
            title: 'Location shift',
            startAtUtc: '2026-07-13T16:00:00Z',
            endAtUtc: '2026-07-14T00:00:00Z',
            timeZoneId: 'America/Vancouver',
            locationId: 12,
            statusTypeCode: 'Active',
            userIds: [],
          },
        ],
      }),
    );
    const getApiUsers = vi.fn().mockReturnValue(createFetchResult({ value: [] }));

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions,
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftsSeries,
      getApiSchedulingShiftsEntries,
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({ getApiUsers }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      postApiSchedulingAssignmentsEntries: vi.fn(),
      postApiSchedulingAssignmentsSeries: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        initialDate: '2026-07-13',
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { locationId?: number };
      assignmentDefinitionOptions: Array<{ code: number }>;
      shiftEntryOptions: Array<{ code: number }>;
      shiftSeriesOptions: Array<{ code: number }>;
    };

    expect(getApiSchedulingAssignmentDefinitions).toHaveBeenCalledWith(
      { locationId: 12 },
      { options: { immediate: false } },
    );
    const shiftQuery = {
      LocationId: 12,
      StartAtUtc: '2026-07-13T07:00:00Z',
      EndAtUtc: '2026-07-14T07:00:00Z',
    };
    expect(getApiSchedulingShiftsEntries).toHaveBeenCalledWith(shiftQuery, { options: { immediate: false } });
    expect(getApiSchedulingShiftsSeries).toHaveBeenCalledWith(shiftQuery, { options: { immediate: false } });
    expect(getApiSchedulingShiftsEntries).toHaveBeenCalledTimes(1);
    expect(getApiSchedulingShiftsSeries).toHaveBeenCalledTimes(1);
    expect(getApiSchedulingAssignmentDefinitions).toHaveBeenCalledTimes(1);
    expect(getApiUsers).toHaveBeenCalledTimes(1);
    expect(vm.assignmentDefinitionOptions.map((option) => option.code)).toEqual([7]);
    expect(vm.shiftEntryOptions.map((option) => option.code)).toEqual([42]);
    expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual([200]);

    vm.formData.locationId = 13;
    await flushPromises();

    expect(getApiSchedulingShiftsEntries).toHaveBeenCalledTimes(2);
    expect(getApiSchedulingShiftsSeries).toHaveBeenCalledTimes(2);
    expect(getApiSchedulingShiftsEntries).toHaveBeenLastCalledWith(
      { ...shiftQuery, LocationId: 13 },
      { options: { immediate: false } },
    );
    expect(getApiSchedulingShiftsSeries).toHaveBeenLastCalledWith(
      { ...shiftQuery, LocationId: 13 },
      { options: { immediate: false } },
    );
    expect(getApiSchedulingAssignmentDefinitions).toHaveBeenCalledTimes(2);
    expect(getApiSchedulingAssignmentDefinitions).toHaveBeenLastCalledWith(
      { locationId: 13 },
      { options: { immediate: false } },
    );
    expect(getApiUsers).toHaveBeenCalledTimes(2);
    expect(getApiUsers).toHaveBeenLastCalledWith(
      { IsEnabled: true, LocationId: 13 },
      {
        fetchOptions: { signal: expect.any(AbortSignal) },
        options: { immediate: false },
      },
    );

    wrapper.unmount();
  });

  it('keeps a draft assignment editable when it is linked to an active shift', async () => {
    const getAssignmentDefinitionsExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftSeriesExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftEntriesExecute = vi.fn().mockResolvedValue(undefined);
    const getUsersExecute = vi.fn().mockResolvedValue(undefined);
    const getAssignmentEntryExecute = vi.fn().mockResolvedValue(undefined);

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 7,
              name: 'Court coverage',
              categoryId: 10,
              subCategoryId: 20,
              locationId: 12,
              defaultCapacity: 1,
              effectiveDateUtc: '2026-07-01T00:00:00Z',
              expiryDateUtc: null,
            },
          ],
        },
        error: { value: null },
        execute: getAssignmentDefinitionsExecute,
      }),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftsSeries: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getShiftSeriesExecute,
      }),
      getApiSchedulingShiftsEntries: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 42,
              title: 'Published shift',
              startAtUtc: '2026-07-13T16:00:00Z',
              endAtUtc: '2026-07-14T00:00:00Z',
              timeZoneId: 'America/Vancouver',
              locationId: 12,
              statusTypeCode: 'Active',
              userIds: ['00000000-0000-0000-0000-000000000001'],
            },
          ],
        },
        error: { value: null },
        execute: getShiftEntriesExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: '00000000-0000-0000-0000-000000000001',
              firstName: 'Alex',
              lastName: 'Alpha',
            },
          ],
        },
        error: { value: null },
        execute: getUsersExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      getApiSchedulingAssignmentsEntriesId: vi.fn().mockReturnValue(
        createFetchResult({
          value: {
            id: 257,
            assignmentDefinitionId: 7,
            title: 'Court coverage',
            startAtUtc: '2026-07-13T16:00:00Z',
            endAtUtc: '2026-07-14T00:00:00Z',
            timeZoneId: 'America/Vancouver',
            locationId: 12,
            categoryId: 10,
            subCategoryId: 20,
            capacity: 1,
            statusTypeCode: 'Draft',
            assignmentLinks: [
              {
                shiftEntryId: 42,
                assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
              },
            ],
          },
          execute: getAssignmentEntryExecute,
        }),
      ),
      getApiSchedulingAssignmentsSeriesId: vi.fn(),
      postApiSchedulingAssignmentsEntries: vi.fn(),
      postApiSchedulingAssignmentsSeries: vi.fn(),
      putApiSchedulingAssignmentsEntriesId: vi.fn(),
      putApiSchedulingAssignmentsSeriesId: vi.fn(),
      postApiSchedulingAssignmentsEntriesIdExpire: vi.fn(),
      postApiSchedulingAssignmentsSeriesIdExpire: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        mode: 'view',
        assignmentEntryId: 257,
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    expect(document.body.textContent).not.toContain(
      'This assignment has been published, and cannot be edited or deleted, only cancelled',
    );
    expect(document.body.textContent).toContain(
      'This assignment is linked to a published shift. New links to published shifts cannot be added.',
    );
    expect(Array.from(document.querySelectorAll('button')).some((button) => button.textContent === 'Edit')).toBe(true);
    expect(Array.from(document.querySelectorAll('button')).some((button) => button.textContent === 'Delete')).toBe(
      true,
    );

    wrapper.unmount();
  });

  it('blocks new assignment links to published shift entries', async () => {
    const getAssignmentDefinitionsExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftSeriesExecute = vi.fn().mockResolvedValue(undefined);
    const getShiftEntriesExecute = vi.fn().mockResolvedValue(undefined);
    const getUsersExecute = vi.fn().mockResolvedValue(undefined);

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getAssignmentDefinitionsExecute,
      }),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      getApiSchedulingShiftsSeries: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getShiftSeriesExecute,
      }),
      getApiSchedulingShiftsEntries: vi.fn().mockReturnValue({
        data: {
          value: [
            {
              id: 42,
              title: 'Published shift',
              startAtUtc: '2026-07-13T16:00:00Z',
              endAtUtc: '2026-07-14T00:00:00Z',
              timeZoneId: 'America/Vancouver',
              locationId: 12,
              statusTypeCode: 'Active',
              userIds: ['00000000-0000-0000-0000-000000000001'],
            },
          ],
        },
        error: { value: null },
        execute: getShiftEntriesExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers: vi.fn().mockReturnValue({
        data: { value: [] },
        error: { value: null },
        execute: getUsersExecute,
      }),
    }));
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      postApiSchedulingAssignmentsEntries: vi.fn(),
      postApiSchedulingAssignmentsSeries: vi.fn(),
    }));

    const { default: CalendarSchedulingAssignmentModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        initialDate: '2026-07-13',
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { shiftEntryLinks?: Array<{ shiftEntryId: number }> };
      updateSelectedShiftEntry: (value: number) => void;
    };

    vm.updateSelectedShiftEntry(42);
    await flushPromises();

    expect(vm.formData.shiftEntryLinks).toEqual([]);
    expect(document.body.textContent).toContain(
      'Shift already published. To link a new assignment, please create a new shift.',
    );

    wrapper.unmount();
  });
});
