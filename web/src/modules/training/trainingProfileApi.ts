import { useFetchAPI, type UseFetchAPIOptions } from '@/api-access/useFetchAPI';

export type TrainingProfileResponse = {
  id: number;
  code: string;
  name: string;
  effectiveDate: string;
  expiryDate?: string | null;
};

type LookupCodeResponse = {
  id?: number | null;
  code: string;
  description: string;
  effectiveDate: string;
  expiryDate?: string | null;
};

export const useTrainingProfiles = (options?: UseFetchAPIOptions) => {
  return useFetchAPI<LookupCodeResponse[]>(
    {
      url: '/api/lookup/TrainingProfiles',
      method: 'GET',
    },
    {
      options: {
        afterFetch(ctx) {
          const data = (ctx.data as LookupCodeResponse[] | null | undefined) ?? [];
          ctx.data = data
            .filter((lookup) => lookup.id != null)
            .map((lookup) => ({
              id: lookup.id as number,
              code: lookup.code,
              name: lookup.description,
              effectiveDate: lookup.effectiveDate,
              expiryDate: lookup.expiryDate ?? null,
            }));
          return ctx;
        },
      },
      ...options,
    },
  );
};
