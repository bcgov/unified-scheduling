import type { StatMetricResponse } from '@/api-access/generated/models';

export function isOvertimeMetric(metric: StatMetricResponse): boolean {
  return metric.unitOfMeasure === 'hours' && metric.isOvertime === true;
}

export function isRegularMetric(metric: StatMetricResponse): boolean {
  return metric.unitOfMeasure === 'hours' && !metric.isOvertime;
}

export function isIntegerMetric(metric: StatMetricResponse): boolean {
  const u = metric.unitOfMeasure;
  return u === 'count' || u === 'count (received/concluded)';
}

export function isHoursMetric(metric: StatMetricResponse): boolean {
  return metric.unitOfMeasure === 'hours';
}
