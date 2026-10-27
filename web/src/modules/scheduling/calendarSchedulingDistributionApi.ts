import { useFetchAPI } from '@/api-access/useFetchAPI';

export type ScheduleDistributionView = 'schedule' | 'assignments';
export type ScheduleDistributionPeriod = 'day' | 'week' | 'work-week';

export interface ScheduleDistributionRequest {
  context: {
    view: ScheduleDistributionView;
    locationId: number;
    startDate: string;
    endDate: string;
    period: ScheduleDistributionPeriod;
    includeAssignmentTimes?: boolean;
    viewportWidth?: number;
    viewportHeight?: number;
  };
  recipientUserIds: string[];
}

export interface ScheduleDistributionResult {
  recipientCount?: number;
}

export function postScheduleDistribution(request: ScheduleDistributionRequest) {
  return useFetchAPI<ScheduleDistributionResult>(
    {
      url: '/api/scheduling/distribute',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: request,
    },
    { options: { immediate: false } },
  );
}
