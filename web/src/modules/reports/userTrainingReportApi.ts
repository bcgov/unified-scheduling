// TODO: Have the generic report API return a more typed response so we can leverage Orval

import { computed, unref, type MaybeRef } from 'vue';
import { useFetchAPI } from '@/api-access/useFetchAPI';

export type UserTrainingReportItem = {
  userDisplayName: string;
  regionName?: string | null;
  locationName?: string | null;
  trainingId: number;
  trainingCode: string;
  trainingDescription: string;
  awardedOn?: string | null;
  endingOn?: string | null;
  expiryDate?: string | null;
  status: string;
  version?: number | null;
  noticeState: string;
  notes?: string | null;
  hasMissingMandatoryTrainingAssignment: boolean;
};

export type UserTrainingReportResponse = {
  rows: UserTrainingReportItem[];
  page: number;
  pageSize: number;
  totalRows: number;
};

export type UserTrainingReportQuery = {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  userId?: string;
  regionId?: number;
  locationId?: number;
  trainingId?: number;
  trainingCode?: string;
  status?: 'active' | 'expired' | 'notTaken';
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
      regionId: resolvedQuery.regionId,
      locationId: resolvedQuery.locationId,
      trainingId: resolvedQuery.trainingId,
      trainingCode: resolvedQuery.trainingCode || undefined,
      status: resolvedQuery.status,
      startDate: resolvedQuery.startDate || undefined,
      endDate: resolvedQuery.endDate || undefined,
    };
  });

  return useFetchAPI<UserTrainingReportResponse>(
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
