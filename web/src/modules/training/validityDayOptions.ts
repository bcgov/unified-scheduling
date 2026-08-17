import type { SelectOption } from '@/types/select';

const ONE_TIME_CODE = 'one-time';
const THREE_MONTHS_CODE = 'three-months';
const SIX_MONTHS_CODE = 'six-months';
const ANNUAL_CODE = 'annual';
const TWO_YEARS_CODE = 'two-years';
const THREE_YEARS_CODE = 'three-years';
const FOUR_YEARS_CODE = 'four-years';
const FIVE_YEARS_CODE = 'five-years';
const CUSTOM_CODE_PREFIX = 'custom-';

const validityCodeToDays = new Map<string, number | null>([
  [ONE_TIME_CODE, null],
  [THREE_MONTHS_CODE, 90],
  [SIX_MONTHS_CODE, 180],
  [ANNUAL_CODE, 365],
  [TWO_YEARS_CODE, 365 * 2],
  [THREE_YEARS_CODE, 365 * 3],
  [FOUR_YEARS_CODE, 365 * 4],
  [FIVE_YEARS_CODE, 365 * 5],
]);

const baseValidityDayOptions: SelectOption[] = [
  { code: ONE_TIME_CODE, description: 'One time' },
  { code: THREE_MONTHS_CODE, description: '3 months' },
  { code: SIX_MONTHS_CODE, description: '6 months' },
  { code: ANNUAL_CODE, description: 'Annually' },
  { code: TWO_YEARS_CODE, description: 'Every 2 years' },
  { code: THREE_YEARS_CODE, description: 'Every 3 years' },
  { code: FOUR_YEARS_CODE, description: 'Every 4 years' },
  { code: FIVE_YEARS_CODE, description: 'Every 5 years' },
];

const getCustomCode = (validityDays: number) => `${CUSTOM_CODE_PREFIX}${validityDays}`;

export const annualValidityDayCode = ANNUAL_CODE;

export const getValidityDayCodeFromDays = (validityDays: number | null | undefined): string => {
  if (validityDays == null) {
    return ONE_TIME_CODE;
  }

  for (const [code, days] of validityCodeToDays.entries()) {
    if (days === validityDays) {
      return code;
    }
  }

  return getCustomCode(validityDays);
};

export const getValidityDaysFromCode = (code: string): number | null | undefined => {
  if (validityCodeToDays.has(code)) {
    return validityCodeToDays.get(code);
  }

  if (!code.startsWith(CUSTOM_CODE_PREFIX)) {
    return undefined;
  }

  const parsed = Number(code.slice(CUSTOM_CODE_PREFIX.length));
  return Number.isInteger(parsed) && parsed >= 0 ? parsed : undefined;
};

export const getValidityDayOptions = (currentValidityDays: number | null | undefined): SelectOption[] => {
  if (currentValidityDays == null) {
    return baseValidityDayOptions;
  }

  const hasMatch = [...validityCodeToDays.values()].some((days) => days === currentValidityDays);
  if (hasMatch) {
    return baseValidityDayOptions;
  }

  return [
    ...baseValidityDayOptions,
    {
      code: getCustomCode(currentValidityDays),
      description: `${currentValidityDays} days (custom)`,
    },
  ];
};

export const defaultValidityDayCode = ONE_TIME_CODE;
