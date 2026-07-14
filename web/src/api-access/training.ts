import { useFetchAPI } from './useFetchAPI';

type FetchOptions = Parameters<typeof useFetchAPI>[1];

export type TrainingRequest = {
  code: string;
  description: string;
  mandatory: boolean;
  validityDays: number | null;
  advanceNoticeDays: number | null;
  rotating: boolean;
  trainingCategoryId: number | null;
  order: number;
};

export type TrainingResponse = {
  id: number;
  code: string;
  description: string;
  effectiveDate: string;
  expiryDate: string | null;
  mandatory: boolean;
  validityDays: number | null;
  advanceNoticeDays: number | null;
  rotating: boolean;
  trainingCategoryId: number | null;
  trainingCategoryName: string | null;
  order: number;
  createdOn: string;
  updatedOn: string | null;
};

export const getApiTrainings = (options?: FetchOptions) => {
  return useFetchAPI<TrainingResponse[]>({ url: '/api/lookup/trainings', method: 'GET' }, options);
};

export const postApiTrainings = (data: TrainingRequest, options?: FetchOptions) => {
  return useFetchAPI<TrainingResponse>(
    {
      url: '/api/lookup/trainings',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data,
    },
    options,
  );
};

export const putApiTrainingsId = (id: number, data: TrainingRequest, options?: FetchOptions) => {
  return useFetchAPI<TrainingResponse>(
    {
      url: `/api/lookup/trainings/${id}`,
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      data,
    },
    options,
  );
};

export type TrainingMoveOrderRequest = {
  newOrder: number;
};

export const patchApiTrainingsIdOrder = (id: number, data: TrainingMoveOrderRequest, options?: FetchOptions) => {
  return useFetchAPI<TrainingResponse>(
    {
      url: `/api/lookup/trainings/${id}/order`,
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      data,
    },
    options,
  );
};
