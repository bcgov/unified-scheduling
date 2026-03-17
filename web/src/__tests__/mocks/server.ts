import { setupServer } from 'msw/node';

import { getAuthMock } from '@/api-access/generated/auth/auth.msw';
import { getHealthMock } from '@/api-access/generated/health/health.msw';
import { getStatsMock } from '@/api-access/generated/stats/stats.msw';
import { getUsersMock } from '@/api-access/generated/users/users.msw';

export const server = setupServer(...getAuthMock(), ...getHealthMock(), ...getStatsMock(), ...getUsersMock());
