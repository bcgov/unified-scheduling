import { afterEach, describe, expect, it, vi } from 'vitest';
import { IANAZone } from 'luxon';
import { http, HttpResponse } from 'msw';
import { server } from '@/__tests__/mocks/server';
import {
  assertSchedulingTimeZoneCompatibility,
  SchedulingTimeZoneCompatibilityError,
} from '@/modules/scheduling/schedulingTimeZoneCompatibility';
import * as shifts from '@/modules/scheduling/calendarSchedulingShiftApi';
import * as assignments from '@/modules/scheduling/calendarSchedulingAssignmentApi';
import * as leave from '@/modules/leave/api/leaveApi';
import { useSchedulingAssignmentMutation } from '@/modules/scheduling/useSchedulingAssignmentMutation';

afterEach(() => vi.restoreAllMocks());

describe('scheduling time-zone compatibility', () => {
  it.each([
    ['America/Vancouver', -420],
    ['America/Edmonton', -360],
    ['America/Dawson_Creek', -420],
    ['America/Creston', -420],
  ] as const)('accepts current rules for %s using a fixed UTC probe', (zone, offset) => {
    const resolveOffset = vi.fn(() => offset);
    expect(() => assertSchedulingTimeZoneCompatibility(zone, resolveOffset)).not.toThrow();
    expect(resolveOffset).toHaveBeenCalledWith(zone, Date.UTC(2027, 0, 15, 12));
  });

  it.each([
    ['America/Vancouver', -480],
    ['America/Edmonton', -420],
    ['America/Dawson_Creek', NaN],
    ['America/Creston', Infinity],
  ] as const)('rejects obsolete or unresolvable rules for %s', (zone, offset) => {
    expect(() => assertSchedulingTimeZoneCompatibility(zone, () => offset)).toThrow(
      SchedulingTimeZoneCompatibilityError,
    );
  });

  it('fails closed when the browser cannot resolve an affected zone', () => {
    expect(() =>
      assertSchedulingTimeZoneCompatibility('America/Vancouver', () => {
        throw new RangeError('Unsupported zone');
      }),
    ).toThrow('No changes were saved.');
  });

  it.each(['America/Toronto', 'Europe/London', 'UTC', null, undefined])('does not block %s', (zone) => {
    const resolveOffset = vi.fn();
    expect(() => assertSchedulingTimeZoneCompatibility(zone, resolveOffset)).not.toThrow();
    expect(resolveOffset).not.toHaveBeenCalled();
  });
});

const body = { timeZoneId: 'America/Vancouver', title: 'Coverage', concurrencyToken: 7 };
const writes = [
  { label: 'create shift entry', save: () => shifts.createShiftEntry(body), path: 'shifts/entries', method: 'POST' },
  { label: 'create shift series', save: () => shifts.createShiftSeries(body), path: 'shifts/series', method: 'POST' },
  {
    label: 'update shift entry',
    save: () => shifts.updateShiftEntry(41, body),
    path: 'shifts/entries/41',
    method: 'PUT',
  },
  {
    label: 'update shift series',
    save: () => shifts.updateShiftSeries(42, body),
    path: 'shifts/series/42',
    method: 'PUT',
  },
  {
    label: 'create assignment entry',
    save: () => assignments.createAssignmentEntry(body),
    path: 'assignments/entries',
    method: 'POST',
  },
  {
    label: 'create assignment series',
    save: () => assignments.createAssignmentSeries(body),
    path: 'assignments/series',
    method: 'POST',
  },
  {
    label: 'update assignment entry',
    save: () => assignments.updateAssignmentEntry(41, body),
    path: 'assignments/entries/41',
    method: 'PUT',
  },
  {
    label: 'update assignment series',
    save: () => assignments.updateAssignmentSeries(42, body),
    path: 'assignments/series/42',
    method: 'PUT',
  },
  { label: 'create leave entry', save: () => leave.createLeaveEntry(body), path: 'leave/entries', method: 'POST' },
  { label: 'create leave series', save: () => leave.createLeaveSeries(body), path: 'leave/series', method: 'POST' },
  {
    label: 'update leave entry',
    save: () => leave.updateLeaveEntry(41, body),
    path: 'leave/entries/41',
    method: 'PUT',
  },
  {
    label: 'update leave series',
    save: () => leave.updateLeaveSeries(42, body),
    path: 'leave/series/42',
    method: 'PUT',
  },
];

describe('scheduling API compatibility enforcement', () => {
  it.each(writes)('blocks $label before HTTP and permits it with current rules', async ({ save, path, method }) => {
    const requests: Array<{ path: string; method: string; body: unknown }> = [];
    server.use(
      http.all('*/api/scheduling/*', async ({ request }) => {
        requests.push({ path: new URL(request.url).pathname, method: request.method, body: await request.json() });
        return HttpResponse.json({ id: 41, concurrencyToken: 8 });
      }),
    );
    const offset = vi.spyOn(IANAZone.prototype, 'offset').mockReturnValue(-480);

    await expect(save()).rejects.toBeInstanceOf(SchedulingTimeZoneCompatibilityError);
    expect(requests).toEqual([]);

    offset.mockReturnValue(-420);
    const result = await save();
    expect(result.error.value).toBeNull();
    expect(requests).toEqual([{ path: `/api/scheduling/${path}`, method, body }]);
  });

  it('rechecks assignment conflict overrides and preserves the concurrency token', async () => {
    const offset = vi.spyOn(IANAZone.prototype, 'offset').mockReturnValue(-480);
    const request = vi.fn();
    server.use(
      http.put('*/api/scheduling/assignments/entries/41', async ({ request: incoming }) => {
        request(await incoming.json());
        return HttpResponse.json({ id: 41, concurrencyToken: 8 });
      }),
    );
    const conflictOverrides = [
      {
        firstSourceModule: 'scheduling',
        firstEventId: '1',
        secondSourceModule: 'scheduling',
        secondEventId: '2',
        resourceId: 'user-1',
        note: 'Approved',
      },
    ];
    const save = () =>
      useSchedulingAssignmentMutation().save({
        payload: { kind: 'entry', body },
        isEdit: true,
        assignmentEntryId: 41,
        concurrencyToken: 7,
        conflictOverrides,
      });
    await expect(save()).rejects.toBeInstanceOf(SchedulingTimeZoneCompatibilityError);
    expect(request).not.toHaveBeenCalled();
    offset.mockReturnValue(-420);
    await save();
    expect(request).toHaveBeenCalledWith({ ...body, conflictOverrides });
  });

  it('leaves reads, deletion, cancellation and publishing available on obsolete browsers', async () => {
    vi.spyOn(IANAZone.prototype, 'offset').mockReturnValue(-480);
    const request = vi.fn();
    server.use(
      http.all('*/api/scheduling/*', ({ request: incoming }) => {
        request(incoming.method);
        return HttpResponse.json({ id: 41 });
      }),
    );
    for (const action of [
      () => shifts.loadShiftEntry(41),
      () => shifts.loadShiftSeries(42),
      () => shifts.deleteShiftEntry(41),
      () => shifts.deleteShiftSeries(42),
      () => shifts.cancelShiftEntry(41),
      () => shifts.cancelShiftSeries(42),
      () => shifts.publishShiftEntry(41),
      () => shifts.publishShiftSeries(42),
      () => assignments.loadAssignmentEntry(41),
      () => assignments.loadAssignmentSeriesById(42),
      () => assignments.deleteAssignmentEntry(41),
      () => assignments.deleteAssignmentSeries(42),
      () => assignments.expireAssignmentEntry(41),
      () => assignments.expireAssignmentSeries(42),
      () => leave.getLeaveEntry(41),
      () => leave.getLeaveSeries(42),
      () => leave.deleteLeaveEntry(41),
      () => leave.deleteLeaveSeries(42),
    ]) {
      const result = await action();
      expect(result.error.value).toBeNull();
    }
    expect(request).toHaveBeenCalledTimes(18);
  });
});
