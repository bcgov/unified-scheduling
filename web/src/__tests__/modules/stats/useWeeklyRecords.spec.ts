import { afterEach, describe, expect, it } from 'vitest';
import { defineComponent, h, ref } from 'vue';
import { mount, type VueWrapper } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import type {
  SaveDayRequest,
  StatCategoryResponse,
  StatMetricResponse,
  StatRecordResponse,
  SubCategoryMetricResponse,
  SubCategoryResponse,
} from '@/api-access/generated/models';
import { useWeeklyRecords } from '@/modules/stats/composables/useWeeklyRecords';

describe('useWeeklyRecords with flat Stats subcategories', () => {
  let wrapper: VueWrapper | undefined;
  afterEach(() => wrapper?.unmount());

  async function createRecords() {
    const app = await createTestApp({ loadConfig: false });
    let records!: ReturnType<typeof useWeeklyRecords>;
    wrapper = mount(
      defineComponent({
        setup() {
          records = useWeeklyRecords(
            '2026-11-01',
            ref(1),
            ref(null),
            3,
            ref<SubCategoryResponse[]>([
              { id: 123, categoryId: 27, name: 'Male - Provincial Adult' },
              { id: 124, categoryId: 27, name: 'Male - Federal Adult' },
            ]),
            ref<StatCategoryResponse[]>([{ id: 27, groupId: 3, name: 'Transports Air' }]),
            ref<SubCategoryMetricResponse[]>([
              { id: 461, subCategoryId: 123, metricId: 36 },
              { id: 462, subCategoryId: 123, metricId: 38 },
              { id: 465, subCategoryId: 124, metricId: 36 },
            ]),
            ref<StatMetricResponse[]>([
              { id: 36, name: 'Level 1 Air', unitOfMeasure: 'count' },
              { id: 38, name: 'Level 2 Air', unitOfMeasure: 'count' },
            ]),
          );
          return () => h('div');
        },
      }),
      { global: { plugins: app.mountPlugins } },
    );
    return records;
  }

  it('edits and reloads distinct demographic cells using SCM IDs and preserves existing record IDs', async () => {
    let stored: StatRecordResponse[] = [
      { id: 1, dateFrom: '2026-11-02', subCategoryMetricId: 418, value: 37, status: 'Draft' },
      { id: 2, dateFrom: '2026-11-02', subCategoryMetricId: 461, value: 2, status: 'Draft' },
      { id: 3, dateFrom: '2026-11-02', subCategoryMetricId: 462, value: 4, status: 'Draft' },
    ];
    let saved: SaveDayRequest | undefined;
    server.use(
      http.get('*/api/stats/records', () => HttpResponse.json(stored)),
      http.put('*/api/stats/records/day', async ({ request }) => {
        saved = (await request.json()) as SaveDayRequest;
        // Mirror the backend contract: omitted retired records survive the save.
        stored = [
          { ...stored[0]!, status: saved.status },
          ...(saved.records ?? []).map((record, index) => ({
            ...record,
            id: record.id ?? 10 + index,
            dateFrom: saved!.date,
            status: saved!.status,
          })),
        ];
        return HttpResponse.json(stored);
      }),
    );
    const records = await createRecords();
    await records.loadWeek();
    const assignments = records.dayAssignmentsMap.value['2026-11-02']!;
    expect(assignments).toHaveLength(1);
    expect(assignments[0]).toMatchObject({
      categoryId: 27,
      subCategoryId: 123,
      metricValues: { 461: '2', 462: '4' },
      existingRecordIds: { 461: 2, 462: 3 },
    });
    assignments[0]!.metricValues[461] = '9';
    assignments.push({
      ...records.createEmptyAssignment(3),
      categoryId: 27,
      subCategoryId: 124,
      metricValues: { 465: '5' },
    });

    expect(await records.saveDay('2026-11-02', assignments, 'Submitted', 3)).toBeNull();
    expect(saved).toMatchObject({ locationId: 1, groupId: 3, date: '2026-11-02', status: 'Submitted' });
    expect(saved?.records).toEqual([
      { id: 2, subCategoryMetricId: 461, value: 9, comment: null },
      { id: 3, subCategoryMetricId: 462, value: 4, comment: null },
      { id: null, subCategoryMetricId: 465, value: 5, comment: null },
    ]);
    expect(records.dayAssignmentsMap.value['2026-11-02']).toHaveLength(2);
    expect(records.dayAssignmentsMap.value['2026-11-02']?.map((a) => [a.subCategoryId, a.metricValues])).toEqual([
      [123, { 461: '9', 462: '4' }],
      [124, { 465: '5' }],
    ]);
    expect(records.dayStatusMap.value['2026-11-02']).toBe('Submitted');
  });

  it('does not reconstruct an invalid empty assignment from archived-only historical records', async () => {
    server.use(
      http.get('*/api/stats/records', () =>
        HttpResponse.json([
          { id: 1, dateFrom: '2026-11-02', subCategoryMetricId: 418, value: 37, status: 'Submitted' },
        ]),
      ),
    );
    const records = await createRecords();
    await records.loadWeek();
    expect(records.dayAssignmentsMap.value['2026-11-02']).toEqual([]);
    expect(records.dayStatusMap.value['2026-11-02']).toBe('Submitted');
    expect(records.error.value).toBe('');
  });
});
