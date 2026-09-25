import { useFetchAPI, type UseFetchAPIOptions } from '@/api-access/useFetchAPI';

export type TrainingProfileLookupResponse = {
  id: number;
  code: string;
  name: string;
};

export const useTrainingProfileLookup = (options?: UseFetchAPIOptions) => {
  return useFetchAPI<TrainingProfileLookupResponse[]>(
    {
      url: '/api/training/profile-types',
      method: 'GET',
    },
    options,
  );
};
