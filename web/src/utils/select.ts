import type { SelectOption } from '@/types/select';

export const mapToSelectOptions = <T>(
  source: T[],
  getCode: (item: T) => SelectOption['code'] | null | undefined,
  getDescription: (item: T) => string | null | undefined,
): SelectOption[] => {
  return source
    .map((item) => ({
      code: getCode(item),
      description: getDescription(item),
    }))
    .filter(
      (option): option is SelectOption =>
        option.code !== null &&
        option.code !== undefined &&
        typeof option.description === 'string' &&
        option.description.length > 0,
    )
    .sort((a, b) => a.description.localeCompare(b.description));
};
