import type { TrainingLookupResponse } from '@/api-access/generated/models';
import { useFetchAPI, type UseFetchAPIOptions } from '@/api-access/useFetchAPI';
import { computed, type MaybeRef, unref } from 'vue';

export const useTrainingLookup = (includeExpired: MaybeRef<boolean>, options?: UseFetchAPIOptions) => {
  const params = computed(() => ({ includeExpired: unref(includeExpired) }));

  return useFetchAPI<TrainingLookupResponse[]>(
    {
      url: '/api/lookup/trainings',
      method: 'GET',
      params,
    },
    options,
  );
};

export const expireTrainingLookup = (id: number) =>
  useFetchAPI<TrainingLookupResponse>({
    url: `/api/lookup/trainings/${id}/expire`,
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
  });

export const unexpireTrainingLookup = (id: number) =>
  useFetchAPI<TrainingLookupResponse>({
    url: `/api/lookup/trainings/${id}/unexpire`,
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
  });
