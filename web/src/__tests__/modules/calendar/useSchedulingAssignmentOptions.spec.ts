import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises } from '@vue/test-utils';
import { computed, ref } from 'vue';
import type { ShiftResourceFormData } from '@/modules/scheduling/calendarSchedulingShiftForm';

describe('useSchedulingAssignmentOptions', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it('returns empty options and does not fetch assignment links without an active location', async () => {
    const loadAssignmentEntries = vi.fn();
    const loadAssignmentSeries = vi.fn();

    vi.doMock('@/modules/scheduling/calendarSchedulingAssignmentApi', () => ({
      loadAssignmentEntries,
      loadAssignmentSeries,
    }));

    const { useSchedulingAssignmentOptions } = await import('@/modules/scheduling/useSchedulingAssignmentOptions');
    const formData = ref<ShiftResourceFormData>({
      date: '2026-07-13',
      startTime: '09:00',
      endTime: '17:00',
      repeatMode: 'never',
      publish: 'no',
      cancel: 'no',
    });
    const locationId = ref<number | null>(null);

    const result = useSchedulingAssignmentOptions({
      formData,
      activeLocationId: computed(() => locationId.value),
      activeTimeZoneId: computed(() => 'America/Vancouver'),
      isSeriesScope: computed(() => false),
      onError: vi.fn(),
    });

    await flushPromises();

    expect(result.assignmentEntryOptions.value).toEqual([]);
    expect(result.assignmentSeriesOptions.value).toEqual([]);
    expect(loadAssignmentEntries).not.toHaveBeenCalled();
    expect(loadAssignmentSeries).not.toHaveBeenCalled();
  });

  it('loads assignment entry and series options using the active location', async () => {
    const loadAssignmentEntries = vi.fn().mockResolvedValue({
      data: {
        value: [
          {
            id: 42,
            title: 'Court assignment',
            startAtUtc: '2026-07-13T16:00:00Z',
            endAtUtc: '2026-07-14T00:00:00Z',
            statusTypeCode: 'Active',
          },
        ],
      },
      error: { value: null },
    });
    const loadAssignmentSeries = vi.fn().mockResolvedValue({
      data: {
        value: [
          {
            id: 200,
            title: 'Court assignment series',
            startAtUtc: '2026-07-13T16:00:00Z',
            endAtUtc: '2026-07-14T00:00:00Z',
            statusTypeCode: 'Active',
            entries: [
              {
                id: 420,
                startAtUtc: '2026-07-13T16:00:00Z',
                endAtUtc: '2026-07-14T00:00:00Z',
                statusTypeCode: 'Active',
              },
            ],
          },
        ],
      },
      error: { value: null },
    });

    vi.doMock('@/modules/scheduling/calendarSchedulingAssignmentApi', () => ({
      loadAssignmentEntries,
      loadAssignmentSeries,
    }));

    const { useSchedulingAssignmentOptions } = await import('@/modules/scheduling/useSchedulingAssignmentOptions');
    const formData = ref<ShiftResourceFormData>({
      date: '2026-07-13',
      startTime: '09:00',
      endTime: '17:00',
      repeatMode: 'custom',
      recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=2',
      publish: 'no',
      cancel: 'no',
    });

    const result = useSchedulingAssignmentOptions({
      formData,
      activeLocationId: computed(() => 12),
      activeTimeZoneId: computed(() => 'America/Vancouver'),
      isSeriesScope: computed(() => true),
      onError: vi.fn(),
    });

    await flushPromises();

    expect(loadAssignmentEntries).toHaveBeenCalledWith(
      expect.objectContaining({
        LocationId: 12,
      }),
    );
    expect(loadAssignmentSeries).toHaveBeenCalledWith(
      expect.objectContaining({
        LocationId: 12,
      }),
    );
    expect(result.assignmentEntryOptions.value.map((option) => option.code)).toEqual([42]);
    expect(result.assignmentSeriesOptions.value.map((option) => option.code)).toEqual([200]);
  });

  it('ignores a stale response after the active location changes', async () => {
    let resolveFirstRequest: ((value: unknown) => void) | undefined;
    const firstRequest = new Promise((resolve) => {
      resolveFirstRequest = resolve;
    });
    const loadAssignmentEntries = vi
      .fn()
      .mockReturnValueOnce(firstRequest)
      .mockResolvedValueOnce({
        data: { value: [{ id: 2, title: 'Latest', statusTypeCode: 'Draft' }] },
        error: { value: null },
      });

    vi.doMock('@/modules/scheduling/calendarSchedulingAssignmentApi', () => ({
      loadAssignmentEntries,
      loadAssignmentSeries: vi.fn(),
    }));

    const { useSchedulingAssignmentOptions } = await import('@/modules/scheduling/useSchedulingAssignmentOptions');
    const locationId = ref<number | null>(1);
    const formData = ref<ShiftResourceFormData>({
      date: '2026-07-13',
      startTime: '09:00',
      endTime: '17:00',
      repeatMode: 'never',
      publish: 'no',
      cancel: 'no',
    });
    const result = useSchedulingAssignmentOptions({
      formData,
      activeLocationId: computed(() => locationId.value),
      activeTimeZoneId: computed(() => 'America/Vancouver'),
      isSeriesScope: computed(() => false),
      onError: vi.fn(),
    });

    await flushPromises();
    locationId.value = 2;
    await flushPromises();
    resolveFirstRequest?.({
      data: { value: [{ id: 1, title: 'Stale', statusTypeCode: 'Draft' }] },
      error: { value: null },
    });
    await flushPromises();

    expect(result.assignmentEntryOptions.value.map((option) => option.code)).toEqual([2]);
  });
});
