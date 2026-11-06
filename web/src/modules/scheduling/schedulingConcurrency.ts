import type { Ref } from 'vue';

export const schedulingConcurrencyMessage =
  'This record was changed by someone else while you were editing it. Your changes were not saved. Reload the latest version and review it before saving again.';

export function isSchedulingConcurrencyConflict(result: { statusCode?: Ref<number | null>; data: Ref<unknown> }) {
  const problem = result.data.value;
  return (
    result.statusCode?.value === 409 &&
    problem !== null &&
    typeof problem === 'object' &&
    'code' in problem &&
    problem.code === 'concurrency_conflict'
  );
}
