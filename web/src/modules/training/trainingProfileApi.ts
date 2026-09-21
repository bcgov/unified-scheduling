import { useFetchAPI, type UseFetchAPIOptions } from '@/api-access/useFetchAPI';

export type TrainingProfileLookupResponse = {
  id: number;
  code: string;
  createdOn: string;
  updatedOn?: string | null;
};

export const useTrainingProfileLookup = (options?: UseFetchAPIOptions) => {
  return useFetchAPI<TrainingProfileLookupResponse[]>(
    {
      url: '/api/lookup/training-profiles',
      method: 'GET',
    },
    options,
  );
};
