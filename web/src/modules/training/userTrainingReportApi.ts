import { computed, unref, type MaybeRef } from 'vue';
import type { ReportQueryResult } from '@/api-access/generated/models';
import { useFetchAPI } from '@/api-access/useFetchAPI';

export type UserTrainingReportQuery = {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  userId?: string;
  trainingId?: number;
  trainingCode?: string;
  status?: 'active' | 'expired';
  startDate?: string;
  endDate?: string;
};

export const useUserTrainingReport = (query: MaybeRef<UserTrainingReportQuery>) => {
  const params = computed(() => {
    const resolvedQuery = unref(query);

    return {
      page: resolvedQuery.page ?? 1,
      pageSize: resolvedQuery.pageSize ?? 100,
      sortBy: resolvedQuery.sortBy ?? 'userDisplayName',
      sortDir: resolvedQuery.sortDir ?? 'asc',
      userId: resolvedQuery.userId || undefined,
      trainingId: resolvedQuery.trainingId,
      trainingCode: resolvedQuery.trainingCode || undefined,
      status: resolvedQuery.status,
      startDate: resolvedQuery.startDate || undefined,
      endDate: resolvedQuery.endDate || undefined,
    };
  });

  return useFetchAPI<ReportQueryResult>(
    {
      url: '/api/reports/user-training',
      method: 'GET',
      params,
    },
    {
      options: {
        immediate: false,
      },
    },
  );
};
