import { afterEach, describe, expect, it, vi } from 'vitest';
import { enableAutoUnmount, mount } from '@vue/test-utils';
import { defineComponent, type PropType } from 'vue';
import { http, HttpResponse } from 'msw';
import { Permissions } from '@/api-access/generated/models';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import UserTrainingReport from '@/modules/reports/UserTrainingReport.vue';
import type { UserTrainingReportItem } from '@/modules/reports/userTrainingReportApi';
import UaBtn from '@/shared/components/UaBtn.vue';

const UaDataTableStub = defineComponent({
  name: 'UaDataTable',
  props: {
    items: {
      type: Array as PropType<Record<string, unknown>[]>,
      required: true,
    },
  },
  template: '<div />',
});

enableAutoUnmount(afterEach);
afterEach(() => vi.restoreAllMocks());

describe('UserTrainingReport', () => {
  it('preserves all-day and employee-local timed dates in rows and CSV regardless of browser timezone', async () => {
    const allDay: UserTrainingReportItem = {
      userDisplayName: 'All Day User',
      trainingId: 1,
      trainingCode: 'FIRST-AID',
      trainingDescription: 'First aid',
      allDay: true,
      awardedOn: '2026-07-20',
      endingOn: '2026-07-22',
      expiryDate: '2026-07-22T23:59:59.999-07:00',
      status: 'Active',
      version: 1,
      noticeState: 'None',
      hasMissingMandatoryTrainingAssignment: false,
    };
    const timed: UserTrainingReportItem = {
      ...allDay,
      userDisplayName: 'Timed User',
      allDay: false,
      awardedOn: '2026-07-19T23:30:00-06:00',
      endingOn: '2026-07-20T00:30:00-06:00',
    };
    const missing: UserTrainingReportItem = {
      ...allDay,
      userDisplayName: 'Missing User',
      allDay: false,
      awardedOn: null,
      endingOn: null,
      expiryDate: null,
      status: 'Not Taken',
      version: null,
      hasMissingMandatoryTrainingAssignment: true,
    };
    server.use(
      http.get('*/api/reports/user-training', () =>
        HttpResponse.json({ rows: [allDay, timed, missing], totalRows: 3 }),
      ),
      ...['/api/users', '/api/region', '/api/location/all', '/api/lookup/trainings'].map((path) =>
        http.get(`*${path}`, () => HttpResponse.json([])),
      ),
    );

    const nativeDateFormat = Date.prototype.toLocaleDateString;
    const localeOptions = { timeZone: 'America/Vancouver' };
    const expiryDate = '2026-07-22';
    vi.spyOn(Date.prototype, 'toLocaleDateString').mockImplementation(function (this: Date) {
      return nativeDateFormat.call(this, 'en-CA', localeOptions);
    });
    const createObjectURL = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:training-report');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {});
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    const app = await createTestApp({ loadConfig: false, permissions: [Permissions.ReportsGenerate] });
    const wrapper = mount(UserTrainingReport, {
      global: {
        plugins: app.mountPlugins,
        stubs: {
          UaCard: { template: '<div><slot /><slot name="actions" /></div>' },
          UaDataTable: UaDataTableStub,
          UaSelect: true,
          UaTextField: true,
        },
      },
    });
    await wrapper
      .findAllComponents(UaBtn)
      .find((button) => button.text() === 'Generate report')!
      .trigger('click');
    await vi.waitFor(() => expect(wrapper.getComponent(UaDataTableStub).props('items')).toHaveLength(3));

    const rows = wrapper.getComponent(UaDataTableStub).props('items');
    expect(rows[0]).toMatchObject({ awardedOn: '2026-07-20', endingOn: '2026-07-22', expiryDate });
    expect(rows[1]).toMatchObject({ awardedOn: '2026-07-19', endingOn: '2026-07-20' });
    expect(rows[2]).toMatchObject({ awardedOn: null, endingOn: null, expiryDate: null, status: 'Not Taken' });

    await wrapper
      .findAllComponents(UaBtn)
      .find((button) => button.text() === 'Export CSV')!
      .trigger('click');
    const blob = createObjectURL.mock.calls[0]![0] as Blob;
    const csvRows = (await blob.text())
      .replace(/^\uFEFF/, '')
      .split('\r\n')
      .map((line) => line.split(','));
    const awardedIndex = csvRows[0]!.indexOf('Awarded On');
    const endingIndex = csvRows[0]!.indexOf('Ending On');
    const expiryIndex = csvRows[0]!.indexOf('Expiry Date');
    expect(csvRows[1]![awardedIndex]).toBe('2026-07-20');
    expect(csvRows[1]![endingIndex]).toBe('2026-07-22');
    expect(csvRows[1]![expiryIndex]).toBe(expiryDate);
    expect(csvRows[2]![awardedIndex]).toBe('2026-07-19');
    expect(csvRows[2]![endingIndex]).toBe('2026-07-20');
    expect(csvRows[3]![awardedIndex]).toBe('');
    expect(csvRows[3]![endingIndex]).toBe('');
  });
});
