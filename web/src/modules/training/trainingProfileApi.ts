import type { TrainingProfileTypeResponse } from '@/api-access/generated/models';
import { getApiTrainingProfileTypes } from '@/api-access/generated/training/training';
import type { UseFetchAPIOptions } from '@/api-access/useFetchAPI';

export type TrainingProfileResponse = Required<Pick<TrainingProfileTypeResponse, 'id' | 'code' | 'name'>>;

const normalizeProfiles = (data: TrainingProfileTypeResponse[] | null | undefined): TrainingProfileResponse[] => {
  return (data ?? [])
    .filter(
      (profile): profile is TrainingProfileResponse =>
        typeof profile.id === 'number' && typeof profile.code === 'string' && typeof profile.name === 'string',
    )
    .map((profile) => ({
      id: profile.id,
      code: profile.code,
      name: profile.name,
    }));
};

export const useTrainingProfiles = (options?: UseFetchAPIOptions) => {
  const userAfterFetch = options?.options?.afterFetch;

  return getApiTrainingProfileTypes({
    ...options,
    options: {
      ...options?.options,
      async afterFetch(ctx) {
        const normalizedContext = {
          ...ctx,
          data: normalizeProfiles(ctx.data as TrainingProfileTypeResponse[] | null | undefined),
        };

        return userAfterFetch ? userAfterFetch(normalizedContext) : normalizedContext;
      },
    },
  });
};
