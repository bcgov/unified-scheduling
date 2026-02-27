// import { getAuthMock } from '@/api-access/generated/auth/auth.msw';
// import { getHealthMock } from '@/api-access/generated/health/health.msw';
// import { getStatsMock } from '@/api-access/generated/stats/stats.msw';
// import { getUnifiedApiMock } from '@/api-access/generated/unified-api/unified-api.msw';
import { getUsersMock } from '@/api-access/generated/users/users.msw';

export const handlers = [
  // ...getAuthMock(),
  // ...getHealthMock(),
  // ...getStatsMock(),
  // ...getUnifiedApiMock(),
  ...getUsersMock(),
];
