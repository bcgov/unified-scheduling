import { useFetchAPI } from './useFetchAPI';

type FetchOptions = Parameters<typeof useFetchAPI>[1];

export interface UserTrainingExpiryDateCalculationRequest {
  trainingId: number;
  awardedOn: string;
}

type QueryParamValue = string | number | boolean | null | undefined;

type UserTrainingExpiryDateCalculationQueryParams = UserTrainingExpiryDateCalculationRequest &
  Record<string, QueryParamValue>;

interface UserTrainingExpiryDateCalculationResponse {
  expiryDate?: string | null;
}

export const getUserTrainingCalculatedExpiryDate = async (
  request: UserTrainingExpiryDateCalculationRequest,
  options?: FetchOptions,
): Promise<string | null> => {
  const { data, error, execute } = useFetchAPI<UserTrainingExpiryDateCalculationResponse>(
    {
      url: '/api/training/user-trainings/expiry-date',
      method: 'GET',
      params: request as UserTrainingExpiryDateCalculationQueryParams,
    },
    {
      ...options,
      options: {
        immediate: false,
        ...options?.options,
      },
    },
  );

  await execute();

  if (error.value) {
    throw error.value;
  }

  return data.value?.expiryDate ?? null;
};
