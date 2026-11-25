// TODO: Replace with the Orval-generated client for GET /api/users/paged once `npm run gen:api` is run against the API.

import type { MaybeRef } from 'vue';
import type { UserResponse } from '@/api-access/generated/models';
import { useFetchAPI } from '@/api-access/useFetchAPI';

export type PagedUserResponse = {
  page: number;
  pageSize: number;
  totalCount: number;
  data: UserResponse[];
};

export type PagedUsersQuery = {
  Search?: string;
  IsEnabled?: boolean;
  LocationId?: number[];
  Page?: number;
  PageSize?: number;
};

export const getApiUsersPaged = (params: MaybeRef<PagedUsersQuery>) =>
  useFetchAPI<PagedUserResponse>({
    url: '/api/users/paged',
    method: 'GET',
    params,
  });
