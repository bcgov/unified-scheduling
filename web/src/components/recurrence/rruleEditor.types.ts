export type Frequency = 'DAILY' | 'WEEKLY' | 'MONTHLY' | 'YEARLY';
export type EndMode = 'never' | 'until' | 'count';
export type MonthlyMode = 'monthday' | 'nth-weekday';
export type Weekday = 'MO' | 'TU' | 'WE' | 'TH' | 'FR' | 'SA' | 'SU';
export type NthWeekdayPosition = 1 | 2 | 3 | 4 | -1;

export interface RRuleEditorModel {
  frequency: Frequency;
  interval: number;
  weekdays: Weekday[];
  monthlyMode: MonthlyMode;
  monthDay?: number;
  nthWeekday?: {
    weekday: Weekday;
    setPosition: NthWeekdayPosition;
  };
  endMode: EndMode;
  until?: Date | null;
  count?: number | null;
}

export interface RRuleParseResult {
  supported: boolean;
  model?: RRuleEditorModel;
  reason?: string;
}

export interface ValidationResult {
  valid: boolean;
  reason?: string;
}

export interface SelectOption<T extends string | number = string> {
  title: string;
  value: T;
}
