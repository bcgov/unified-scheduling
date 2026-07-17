import type { StatMetricResponse } from '@/api-access/generated/models';

export function isOvertimeMetric(metric: StatMetricResponse): boolean {
  return metric.unitOfMeasure === 'hours' && metric.isOvertime === true;
}

export function isRegularMetric(metric: StatMetricResponse): boolean {
  return metric.unitOfMeasure === 'hours' && !metric.isOvertime;
}
