import { useFetchAPI } from './useFetchAPI';

type SecondParameter<T extends (...args: never) => unknown> = Parameters<T>[1];

export interface ActingPositionResponseDto {
  id: number;
  userId: string;
  positionTypeCode: string;
  positionTypeDescription: string;
  effectiveDate: string;
  expiryDate?: string | null;
  expiryReason?: string | null;
  comment?: string | null;
}

export interface ActingPositionRequestDto {
  positionTypeCode: string;
  effectiveDate: string;
  expiryDate?: string | null;
  comment?: string | null;
}

export interface ExpireActingPositionRequestDto {
  actingPositionId: number;
  expiryReason: string;
}

export const getApiUsersIdActingPositions = (
  userId: string,
  options?: SecondParameter<typeof useFetchAPI<ActingPositionResponseDto[]>>,
) => {
  return useFetchAPI<ActingPositionResponseDto[]>(
    { url: `/api/users/${userId}/acting-positions`, method: 'GET' },
    options,
  );
};

export const postApiUsersIdActingPositions = (
  userId: string,
  request: ActingPositionRequestDto,
  options?: SecondParameter<typeof useFetchAPI<ActingPositionResponseDto>>,
) => {
  return useFetchAPI<ActingPositionResponseDto>(
    {
      url: `/api/users/${userId}/acting-positions`,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: request,
    },
    options,
  );
};

export const putApiUsersIdActingPositionsId = (
  userId: string,
  actingPositionId: number,
  request: ActingPositionRequestDto,
  options?: SecondParameter<typeof useFetchAPI<ActingPositionResponseDto>>,
) => {
  return useFetchAPI<ActingPositionResponseDto>(
    {
      url: `/api/users/${userId}/acting-positions/${actingPositionId}`,
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      data: request,
    },
    options,
  );
};

export const postApiUsersIdActingPositionsExpire = (
  userId: string,
  request: ExpireActingPositionRequestDto,
  options?: SecondParameter<typeof useFetchAPI<ActingPositionResponseDto>>,
) => {
  return useFetchAPI<ActingPositionResponseDto>(
    {
      url: `/api/users/${userId}/acting-positions/expire`,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: request,
    },
    options,
  );
};
