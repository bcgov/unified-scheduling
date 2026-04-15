import { useFetchAPI } from './useFetchAPI';

type FetchOptions = Parameters<typeof useFetchAPI>[1];

export interface StatGroupResponse {
  id: number;
  name: string;
  displayOrder: number;
}

export interface StatCategoryResponse {
  id: number;
  groupId: number;
  name: string;
  isArchived: boolean;
  isHighSecurity: boolean;
  displayOrder: number;
}

export interface SubCategoryResponse {
  id: number;
  categoryId: number;
  name: string;
  displayOrder: number;
}

export interface StatMetricResponse {
  id: number;
  name: string;
  unitOfMeasure: string;
}

export interface SubCategoryMetricResponse {
  id: number;
  subCategoryId: number;
  metricId: number;
  displayOrder: number;
}

export interface StatRecordRequest {
  dateFrom: string;
  dateTo: string;
  periodType: string;
  locationId: number;
  subCategoryMetricId: number;
  value: number;
  comment?: string;
  status: string;
}

export interface StatRecordResponse {
  id: number;
  dateFrom: string;
  dateTo: string;
  periodType: string;
  locationId: number;
  subCategoryMetricId: number;
  value: number;
  comment?: string;
  status: string;
  createdOn: string;
  createdById?: string;
  updatedOn?: string;
}

export const getApiStatsGroups = (options?: FetchOptions) =>
  useFetchAPI<StatGroupResponse[]>({ url: '/api/stats/groups', method: 'GET' }, options);

export const getApiStatsCategories = (options?: FetchOptions) =>
  useFetchAPI<StatCategoryResponse[]>({ url: '/api/stats/categories', method: 'GET' }, options);

export const getApiStatsSubCategories = (options?: FetchOptions) =>
  useFetchAPI<SubCategoryResponse[]>({ url: '/api/stats/sub-categories', method: 'GET' }, options);

export const getApiStatsMetrics = (options?: FetchOptions) =>
  useFetchAPI<StatMetricResponse[]>({ url: '/api/stats/metrics', method: 'GET' }, options);

export const getApiStatsSubCategoryMetrics = (options?: FetchOptions) =>
  useFetchAPI<SubCategoryMetricResponse[]>(
    { url: '/api/stats/sub-category-metrics', method: 'GET' },
    options,
  );

export const postApiStatsRecordsBatch = (records: StatRecordRequest[], options?: FetchOptions) =>
  useFetchAPI<StatRecordResponse[]>(
    {
      url: '/api/stats/records/batch',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: records,
    },
    options,
  );
