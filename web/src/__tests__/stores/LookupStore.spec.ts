import { beforeEach, describe, expect, it } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { LookupCodeTypes } from '@/api-access/generated/models';
import { useLookupStore } from '@/stores/LookupStore';

const valid = [
  { code: 'Chief Sheriff', description: 'Chief Sheriff', displayOrder: 3 },
  { code: 'Sergeant', description: 'Sergeant', displayOrder: 8 },
  { code: 'Deputy Sheriff', description: 'Deputy Sheriff', displayOrder: 8 },
];

beforeEach(() => setActivePinia(createPinia()));

describe('lookup store', () => {
  it.each([
    ['empty', []],
    ['missing order', [{ code: 'A' }]],
    ['partially missing order', [{ code: 'A', displayOrder: 1 }, { code: 'B' }]],
    ['null order', [{ code: 'A', displayOrder: null }]],
    ['zero order', [{ code: 'A', displayOrder: 0 }]],
    ['negative order', [{ code: 'A', displayOrder: -1 }]],
    ['fractional order', [{ code: 'A', displayOrder: 1.5 }]],
    ['string order', [{ code: 'A', displayOrder: '1' }]],
    ['missing code', [{ displayOrder: 1 }]],
    ['empty code', [{ code: ' ', displayOrder: 1 }]],
    [
      'duplicate code',
      [
        { code: 'A', displayOrder: 1 },
        { code: 'A', displayOrder: 2 },
      ],
    ],
    ['non-array response', { code: 'A', displayOrder: 1 }],
    ['null entry', [null]],
  ])('rejects %s position data without caching it', async (_name, response) => {
    server.use(http.get('*/api/lookup/PositionTypes', () => HttpResponse.json(response)));
    const store = useLookupStore();
    await expect(store.load(LookupCodeTypes.PositionTypes)).rejects.toBeTruthy();
    expect(store.entityMap.PositionTypes).toBeUndefined();
  });

  it.each([
    ['empty', []],
    ['partially invalid', [{ code: 'A', displayOrder: 1 }, { code: 'B' }]],
  ])(
    'retries %s responses on the same store and caches valid nonconsecutive and shared orders',
    async (_name, invalid) => {
      let calls = 0;
      server.use(
        http.get('*/api/lookup/PositionTypes', () => {
          calls++;
          return HttpResponse.json(calls === 1 ? invalid : valid);
        }),
      );
      const store = useLookupStore();
      await expect(store.load(LookupCodeTypes.PositionTypes)).rejects.toBeTruthy();
      await store.load(LookupCodeTypes.PositionTypes);
      await store.load(LookupCodeTypes.PositionTypes);
      expect(calls).toBe(2);
      expect(store.getSelectOptions(LookupCodeTypes.PositionTypes).map((item) => item.code)).toEqual([
        'Chief Sheriff',
        'Deputy Sheriff',
        'Sergeant',
      ]);
    },
  );

  it('shares failures, cleans up, and retries independently from another cached lookup', async () => {
    let release!: () => void;
    let started!: () => void;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    const requestStarted = new Promise<void>((resolve) => {
      started = resolve;
    });
    let calls = 0;
    server.use(
      http.get('*/api/lookup/EventTypes', () => HttpResponse.json([])),
      http.get('*/api/lookup/PositionTypes', async () => {
        calls++;
        if (calls === 1) {
          started();
          await gate;
          return new HttpResponse(null, { status: 500 });
        }
        return HttpResponse.json(valid);
      }),
    );
    const store = useLookupStore();
    const one = store.load(LookupCodeTypes.PositionTypes, LookupCodeTypes.PositionTypes);
    const two = store.load(LookupCodeTypes.PositionTypes);
    const failures = Promise.allSettled([one, two]);
    await requestStarted;
    await store.load(LookupCodeTypes.EventTypes);
    release();
    expect((await failures).map((result) => result.status)).toEqual(['rejected', 'rejected']);
    expect(calls).toBe(1);
    expect(store.entityMap.EventTypes).toEqual({});
    await store.load(LookupCodeTypes.PositionTypes);
    expect(calls).toBe(2);
    expect(store.entityMap.EventTypes).toEqual({});
    expect(store.entityMap.PositionTypes?.Sergeant?.displayOrder).toBe(8);
  });
});
