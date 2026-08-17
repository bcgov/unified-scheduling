import {
  getDefaultRRuleEditorModel,
  modelToRRuleString,
  rruleStringToModel,
} from '@/components/recurrence/rruleEditor.mapper';
import type { RRuleEditorModel } from '@/components/recurrence/rruleEditor.types';
import { validateRRuleEditorModel } from '@/components/recurrence/rruleEditor.validation';
import { describe, expect, it } from 'vitest';

const startDate = '2026-07-31';

function model(overrides: Partial<RRuleEditorModel> = {}): RRuleEditorModel {
  return {
    ...getDefaultRRuleEditorModel(startDate),
    ...overrides,
  };
}

describe('rruleEditor mapper', () => {
  it('defaults to ending after one occurrence', () => {
    const defaultModel = getDefaultRRuleEditorModel(startDate);

    expect(defaultModel.endMode).toBe('count');
    expect(defaultModel.count).toBe(1);
    expect(modelToRRuleString(defaultModel, startDate)).toBe('RRULE:FREQ=WEEKLY;INTERVAL=1;BYDAY=FR;COUNT=1');
  });

  it('generates daily recurrence rules', () => {
    expect(modelToRRuleString(model({ frequency: 'DAILY', endMode: 'never', count: null }), startDate)).toBe(
      'RRULE:FREQ=DAILY;INTERVAL=1',
    );
    expect(
      modelToRRuleString(model({ frequency: 'DAILY', interval: 2, endMode: 'never', count: null }), startDate),
    ).toBe('RRULE:FREQ=DAILY;INTERVAL=2');
  });

  it('generates daily rules with count and until end conditions', () => {
    expect(modelToRRuleString(model({ frequency: 'DAILY', endMode: 'count', count: 10 }), startDate)).toBe(
      'RRULE:FREQ=DAILY;INTERVAL=1;COUNT=10',
    );

    expect(
      modelToRRuleString(model({ frequency: 'DAILY', endMode: 'until', until: new Date(2026, 6, 31) }), startDate),
    ).toBe('RRULE:FREQ=DAILY;INTERVAL=1;UNTIL=20260731T235959Z');
  });

  it('generates weekly recurrence rules with one or multiple weekdays', () => {
    expect(
      modelToRRuleString(model({ frequency: 'WEEKLY', weekdays: ['MO'], endMode: 'never', count: null }), startDate),
    ).toBe('RRULE:FREQ=WEEKLY;INTERVAL=1;BYDAY=MO');

    expect(
      modelToRRuleString(
        model({ frequency: 'WEEKLY', weekdays: ['MO', 'WE', 'FR'], endMode: 'never', count: null }),
        startDate,
      ),
    ).toBe('RRULE:FREQ=WEEKLY;INTERVAL=1;BYDAY=MO,WE,FR');
  });

  it('converts weekly multi-day recurrence count to total rrule occurrences', () => {
    expect(
      modelToRRuleString(
        model({
          frequency: 'WEEKLY',
          weekdays: ['MO', 'TU', 'WE', 'FR'],
          endMode: 'count',
          count: 1,
        }),
        startDate,
      ),
    ).toBe('RRULE:FREQ=WEEKLY;INTERVAL=1;BYDAY=MO,TU,WE,FR;COUNT=4');
  });

  it('defaults the weekly weekday from start date', () => {
    expect(getDefaultRRuleEditorModel('2026-07-31').weekdays).toEqual(['FR']);
  });

  it('defaults to ending on the provided until date when one is supplied', () => {
    const defaultModel = getDefaultRRuleEditorModel('2026-07-31', '2026-08-15');

    expect(defaultModel.endMode).toBe('until');
    expect(defaultModel.count).toBeNull();
    expect(defaultModel.until?.toISOString()).toContain('2026-08-15');
  });

  it('generates monthly recurrence by day of month', () => {
    expect(
      modelToRRuleString(
        model({ frequency: 'MONTHLY', monthlyMode: 'monthday', monthDay: 15, endMode: 'never', count: null }),
        startDate,
      ),
    ).toBe('RRULE:FREQ=MONTHLY;INTERVAL=1;BYMONTHDAY=15');
  });

  it('generates monthly recurrence by nth weekday', () => {
    expect(
      modelToRRuleString(
        model({
          frequency: 'MONTHLY',
          monthlyMode: 'nth-weekday',
          nthWeekday: { weekday: 'TU', setPosition: 2 },
          endMode: 'never',
          count: null,
        }),
        startDate,
      ),
    ).toBe('RRULE:FREQ=MONTHLY;INTERVAL=1;BYDAY=TU;BYSETPOS=2');

    expect(
      modelToRRuleString(
        model({
          frequency: 'MONTHLY',
          monthlyMode: 'nth-weekday',
          nthWeekday: { weekday: 'FR', setPosition: -1 },
          endMode: 'never',
          count: null,
        }),
        startDate,
      ),
    ).toBe('RRULE:FREQ=MONTHLY;INTERVAL=1;BYDAY=FR;BYSETPOS=-1');
  });

  it('generates yearly recurrence from the start month and day', () => {
    expect(modelToRRuleString(model({ frequency: 'YEARLY', endMode: 'never', count: null }), startDate)).toBe(
      'RRULE:FREQ=YEARLY;INTERVAL=1;BYMONTH=7;BYMONTHDAY=31',
    );
  });

  it('parses component-generated rules back into form state', () => {
    const rrule = modelToRRuleString(
      model({
        frequency: 'MONTHLY',
        interval: 2,
        monthlyMode: 'nth-weekday',
        nthWeekday: { weekday: 'TU', setPosition: 2 },
        endMode: 'count',
        count: 5,
      }),
      startDate,
    );

    const parsed = rruleStringToModel(rrule, startDate);

    expect(parsed.supported).toBe(true);
    expect(parsed.model).toMatchObject({
      frequency: 'MONTHLY',
      interval: 2,
      monthlyMode: 'nth-weekday',
      nthWeekday: { weekday: 'TU', setPosition: 2 },
      endMode: 'count',
      count: 5,
    });
  });

  it('converts weekly multi-day rrule count back to recurrence periods', () => {
    const parsed = rruleStringToModel('RRULE:FREQ=WEEKLY;INTERVAL=1;BYDAY=MO,TU,WE,FR;COUNT=4', startDate);

    expect(parsed.supported).toBe(true);
    expect(parsed.model).toMatchObject({
      frequency: 'WEEKLY',
      weekdays: ['MO', 'TU', 'WE', 'FR'],
      endMode: 'count',
      count: 1,
    });
  });

  it('detects unsupported and invalid rules without throwing', () => {
    expect(rruleStringToModel('RRULE:FREQ=WEEKLY;BYHOUR=9', startDate).supported).toBe(false);
    expect(rruleStringToModel('not an rrule', startDate).supported).toBe(false);
  });
});

describe('rruleEditor validation', () => {
  it('rejects invalid weekly, monthly, interval, count, and until states', () => {
    expect(validateRRuleEditorModel(model({ interval: 0 })).valid).toBe(false);
    expect(validateRRuleEditorModel(model({ frequency: 'WEEKLY', weekdays: [] })).valid).toBe(false);
    expect(validateRRuleEditorModel(model({ frequency: 'MONTHLY', monthlyMode: 'monthday', monthDay: 32 })).valid).toBe(
      false,
    );
    expect(validateRRuleEditorModel(model({ endMode: 'count', count: 0 })).valid).toBe(false);
    expect(validateRRuleEditorModel(model({ endMode: 'until', until: null })).valid).toBe(false);
  });
});
