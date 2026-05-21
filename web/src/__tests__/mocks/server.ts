import { setupServer } from 'msw/node';

import { getAuthMock } from '@/api-access/generated/auth/auth.msw';
import { getConfigMock } from '@/api-access/generated/config/config.msw';
import { getHealthMock } from '@/api-access/generated/health/health.msw';
import { getLocationMock } from '@/api-access/generated/location/location.msw';
import { getLookupMock } from '@/api-access/generated/lookup/lookup.msw';
import { getPermissionsMock } from '@/api-access/generated/permissions/permissions.msw';
import { getRegionMock } from '@/api-access/generated/region/region.msw';
import { getUsersMock } from '@/api-access/generated/users/users.msw';
import { getRolesMock } from '@/api-access/generated/roles/roles.msw';
import { getStatCategoriesMock } from '@/api-access/generated/stat-categories/stat-categories.msw';
import { getStatGroupsMock } from '@/api-access/generated/stat-groups/stat-groups.msw';
import { getStatMetricsMock } from '@/api-access/generated/stat-metrics/stat-metrics.msw';
import { getStatRecordsMock } from '@/api-access/generated/stat-records/stat-records.msw';
import { getStatSignoffsMock } from '@/api-access/generated/stat-signoffs/stat-signoffs.msw';
import { getStatsMock } from '@/api-access/generated/stats/stats.msw';
import { getSubCategoriesMock } from '@/api-access/generated/sub-categories/sub-categories.msw';
import { getSubCategoryMetricsMock } from '@/api-access/generated/sub-category-metrics/sub-category-metrics.msw';

export const server = setupServer(
  ...getAuthMock(),
  ...getConfigMock(),
  ...getHealthMock(),
  ...getLocationMock(),
  ...getLookupMock(),
  ...getPermissionsMock(),
  ...getRegionMock(),
  ...getRolesMock(),
  ...getStatCategoriesMock(),
  ...getStatGroupsMock(),
  ...getStatMetricsMock(),
  ...getStatRecordsMock(),
  ...getStatSignoffsMock(),
  ...getStatsMock(),
  ...getSubCategoriesMock(),
  ...getSubCategoryMetricsMock(),
  ...getUsersMock(),
);
