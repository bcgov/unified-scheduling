import { describe, expect, it } from 'vitest';
import type { UserResponse } from '@/api-access/generated/models';
import {
  GetApiUsersResponseItem,
  GetApiUsersIdResponse,
  PostApiUsersResponse,
  PutApiUsersIdResponse,
  PostApiUsersIdUploadPhotoResponse,
} from '@/api-access/generated/users/users.zod';

describe('generated user response contract', () => {
  it.each([
    ['list', GetApiUsersResponseItem],
    ['get', GetApiUsersIdResponse],
    ['create', PostApiUsersResponse],
    ['update', PutApiUsersIdResponse],
    ['upload photo', PostApiUsersIdUploadPhotoResponse],
  ])('preserves location history in the %s response', (_name, schema) => {
    const user: UserResponse = {
      id: '11111111-1111-4111-8111-111111111111',
      idirName: 'test-user',
      firstName: 'Test',
      lastName: 'User',
      email: 'test@example.com',
      employeeNumber: '12345',
      isEnabled: true,
      gender: 'Male',
      homeLocationId: 2,
      userLocations: [{ id: 5000, locationId: 1, effectiveFrom: null, effectiveUntil: '2026-11-13T08:00:00Z' }],
    };

    expect(schema.parse(user).userLocations).toEqual(user.userLocations);
  });
});
