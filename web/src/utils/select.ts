import type { SelectOption } from '@/types/select';

export const mapToSelectOptions = <T>(
  source: T[],
  getCode: (item: T) => SelectOption['code'] | null | undefined,
  getDescription: (item: T) => string | null | undefined,
  getDisplayOrder?: (item: T) => number | null | undefined,
): SelectOption[] => {
  return source
    .map((item) => ({
      code: getCode(item),
      description: getDescription(item),
      displayOrder: getDisplayOrder?.(item),
    }))
    .filter(
      (option): option is SelectOption & { displayOrder: number | null | undefined } =>
        option.code !== null &&
        option.code !== undefined &&
        typeof option.description === 'string' &&
        option.description.length > 0,
    )
    .sort((a, b) => {
      const displayOrderDifference =
        (a.displayOrder ?? Number.MAX_SAFE_INTEGER) - (b.displayOrder ?? Number.MAX_SAFE_INTEGER);

      return displayOrderDifference || a.description.localeCompare(b.description);
    })
    .map(({ code, description }) => ({ code, description }));
};
