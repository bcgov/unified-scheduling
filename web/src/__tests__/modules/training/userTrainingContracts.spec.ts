import type { UserTrainingRequest } from '@/api-access/generated/models';
import {
  PostApiTrainingUserTrainingsBody,
  PostApiTrainingUserTrainingsResponse,
  PutApiTrainingUserTrainingsIdBody,
  GetApiTrainingUserTrainingsExpiryDateQueryParams,
} from '@/api-access/generated/user-training/user-training.zod';
import { describe, expect, it } from 'vitest';

const request = {
  userId: '95f91fd1-1111-4222-8333-9c0aeb4ca44b',
  trainingId: 1,
  awardedOn: '2026-07-20T22:00:00Z',
  endingOn: '2026-07-21T01:00:00Z',
} satisfies UserTrainingRequest;

describe('user training classification contracts', () => {
  it('accepts legacy and timezone-aware expiry preview queries', () => {
    const query = { TrainingId: 1, AwardedOn: '2026-07-20T00:00:00Z' };
    expect(GetApiTrainingUserTrainingsExpiryDateQueryParams.parse(query)).toEqual(query);
    for (const AllDay of [true, false, null]) {
      const contextual = { ...query, UserId: request.userId, AllDay };
      expect(GetApiTrainingUserTrainingsExpiryDateQueryParams.parse(contextual)).toEqual(contextual);
    }
  });
  it.each([PostApiTrainingUserTrainingsBody, PutApiTrainingUserTrainingsIdBody])(
    'preserves omitted, null, and explicit classifications without applying a client default',
    (schema) => {
      expect(schema.parse(request)).toEqual(request);
      for (const allDay of [null, false, true]) {
        const classified: UserTrainingRequest = { ...request, allDay };
        expect(schema.parse(classified)).toEqual(classified);
      }
    },
  );

  it('keeps response classification non-nullable', () => {
    const schema = PostApiTrainingUserTrainingsResponse.shape.allDay;
    expect(schema.parse(false)).toBe(false);
    expect(schema.parse(true)).toBe(true);
    expect(schema.safeParse(null).success).toBe(false);
  });
});
