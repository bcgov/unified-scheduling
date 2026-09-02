import type { ProposedShiftAssignmentOptionsResponse } from '@/api-access/generated/models/proposedShiftAssignmentOptionsResponse';
import type { ShiftResourceFormData } from '@/modules/scheduling/calendarSchedulingShiftForm';
import { flushPromises } from '@vue/test-utils';
import { computed, ref } from 'vue';
import { beforeEach, describe, expect, it, vi } from 'vitest';

type DeferredResult = {
  execute: ReturnType<typeof vi.fn>;
  data: ReturnType<typeof ref<ProposedShiftAssignmentOptionsResponse | null>>;
  error: ReturnType<typeof ref<Error | null>>;
  response: ReturnType<typeof ref<{ ok: boolean } | null>>;
};

const baseFormData: ShiftResourceFormData = {
  date: '2026-07-13',
  startTime: '09:00',
  endTime: '17:00',
  repeatMode: 'custom',
  recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=2',
  publish: 'no',
  cancel: 'no',
};

describe('useSchedulingAssignmentOptions', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it('sends UTC shift values and maps returned entry and series options', async () => {
    const result = createResult({
      entryOptions: [
        {
          id: 42,
          title: 'Court assignment',
          startAtUtc: '2026-07-13T16:00:00Z',
          endAtUtc: '2026-07-14T00:00:00Z',
        },
      ],
      seriesOptions: [
        {
          id: 200,
          title: 'Court assignment series',
          startAtUtc: '2026-07-13T16:00:00Z',
          endAtUtc: '2026-07-14T00:00:00Z',
        },
      ],
    });
    const postOptions = mockOptionsEndpoint(() => result);
    const assignmentOptions = await createAssignmentOptions();

    await flushPromises();

    expect(postOptions).toHaveBeenCalledWith(
      {
        locationId: 12,
        startAtUtc: '2026-07-13T16:00:00Z',
        endAtUtc: '2026-07-14T00:00:00Z',
        timeZoneId: 'America/Vancouver',
        recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=2',
        isSeriesScope: true,
      },
      { options: { immediate: false } },
    );
    expect(assignmentOptions.assignmentEntryOptions.value).toEqual([
      { code: 42, description: 'Court assignment (Jul 13, 9:00 AM - 5:00 PM)' },
    ]);
    expect(assignmentOptions.assignmentSeriesOptions.value).toEqual([
      { code: 200, description: 'Court assignment series (Jul 13, 9:00 AM - 5:00 PM)' },
    ]);
  });

  it('maps the backend warning flag to the existing warning text', async () => {
    mockOptionsEndpoint(() => createResult({ hasSameDayNonOverlappingAssignments: true }));
    const assignmentOptions = await createAssignmentOptions();

    await flushPromises();

    expect(assignmentOptions.assignmentWarning.value).toBe(
      'One or more matching assignments occur on the same day but do not overlap this shift time.',
    );
  });

  it('does not call the endpoint without complete proposed shift values', async () => {
    const postOptions = mockOptionsEndpoint(() => createResult({}));
    await createAssignmentOptions({ date: undefined });

    await flushPromises();

    expect(postOptions).not.toHaveBeenCalled();
  });

  it('does not let a stale response overwrite a newer response', async () => {
    const first = createPendingResult();
    const second = createResult({ entryOptions: [{ id: 2, title: 'Latest' }] });
    mockOptionsEndpoint(vi.fn().mockReturnValueOnce(first.result).mockReturnValueOnce(second));
    const locationId = ref<number | null>(1);
    const assignmentOptions = await createAssignmentOptions({}, locationId);

    await flushPromises();
    locationId.value = 2;
    await flushPromises();
    first.resolve({ entryOptions: [{ id: 1, title: 'Stale' }] });
    await flushPromises();

    expect(assignmentOptions.assignmentEntryOptions.value.map((option) => option.code)).toEqual([2]);
  });

  it('surfaces backend errors and clears options', async () => {
    const onError = vi.fn();
    mockOptionsEndpoint(() => createResult(null, new Error('Options unavailable')));
    const assignmentOptions = await createAssignmentOptions({}, ref(12), onError);

    await flushPromises();

    expect(onError).toHaveBeenCalledWith('Options unavailable');
    expect(assignmentOptions.assignmentEntryOptions.value).toEqual([]);
    expect(assignmentOptions.assignmentSeriesOptions.value).toEqual([]);
  });

  it('surfaces a missing response instead of treating it as an empty result', async () => {
    const onError = vi.fn();
    mockOptionsEndpoint(() => createResult(null, null, false));
    const assignmentOptions = await createAssignmentOptions({}, ref(12), onError);

    await flushPromises();

    expect(onError).toHaveBeenCalledWith('Failed to load assignments.');
    expect(assignmentOptions.assignmentEntryOptions.value).toEqual([]);
    expect(assignmentOptions.assignmentSeriesOptions.value).toEqual([]);
  });
});

function createResult(
  response: ProposedShiftAssignmentOptionsResponse | null,
  responseError: Error | null = null,
  hasResponse = true,
): DeferredResult {
  return {
    execute: vi.fn().mockResolvedValue(undefined),
    data: ref(response),
    error: ref(responseError),
    response: ref(hasResponse ? { ok: responseError === null } : null),
  };
}

function createPendingResult() {
  const data = ref<ProposedShiftAssignmentOptionsResponse | null>(null);
  let resolveRequest: (() => void) | undefined;
  const execute = vi.fn(
    () =>
      new Promise<void>((resolve) => {
        resolveRequest = resolve;
      }),
  );

  return {
    result: { execute, data, error: ref<Error | null>(null), response: ref({ ok: true }) },
    resolve(response: ProposedShiftAssignmentOptionsResponse) {
      data.value = response;
      resolveRequest?.();
    },
  };
}

function mockOptionsEndpoint(factory: () => DeferredResult) {
  const postOptions = vi.fn(factory);
  vi.doMock('@/api-access/generated/shift-assignment/shift-assignment', () => ({
    postApiSchedulingShiftAssignmentsOptions: postOptions,
  }));
  return postOptions;
}

async function createAssignmentOptions(
  formOverrides: Partial<ShiftResourceFormData> = {},
  locationId = ref<number | null>(12),
  onError = vi.fn(),
) {
  const { useSchedulingAssignmentOptions } = await import('@/modules/scheduling/useSchedulingAssignmentOptions');
  const formData = ref<ShiftResourceFormData>({ ...baseFormData, ...formOverrides });

  return useSchedulingAssignmentOptions({
    formData,
    activeLocationId: computed(() => locationId.value),
    activeTimeZoneId: computed(() => 'America/Vancouver'),
    isSeriesScope: computed(() => true),
    onError,
  });
}
