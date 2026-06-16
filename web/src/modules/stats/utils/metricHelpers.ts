import type { StatMetricResponse } from '@/api-access/generated/models';

export function isOvertimeMetric(metric: StatMetricResponse): boolean {
  return metric.unitOfMeasure === 'hours' && (metric.name?.toLowerCase().includes('overtime') ?? false);
}

export function isRegularMetric(metric: StatMetricResponse): boolean {
  return metric.unitOfMeasure === 'hours' && !(metric.name?.toLowerCase().includes('overtime') ?? false);
}
