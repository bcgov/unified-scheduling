import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import type { RouteRecordRaw } from 'vue-router';
import type { FeatureFlagsResponse, TrainingFeatureFlags } from '@/api-access/generated/models';

describe('reports module', () => {
  beforeEach(() => {
    vi.resetModules();
    setActivePinia(createPinia());
  });

  it('registers report routes', async () => {
    const routes: RouteRecordRaw[] = [];
    const trainingFeatureFlags: TrainingFeatureFlags = { source: 'Training', enabled: true };
    const featureFlags: FeatureFlagsResponse = { Training: trainingFeatureFlags };

    const { registerModule } = await import('@/modules/reports/ReportsModule');

    registerModule(routes, featureFlags);

    expect(routes).toHaveLength(2);
    expect(routes[0]?.path).toBe('/reports/user-training');
    expect(routes[0]?.name).toBe('UserTrainingReport');
    expect(routes[0]?.meta).toMatchObject({ requiresAuth: true });
    expect(routes[1]?.path).toBe('/training/reports/user-training');
  });

  it('registers report routes even when training feature is disabled', async () => {
    const routes: RouteRecordRaw[] = [];
    const trainingFeatureFlags: TrainingFeatureFlags = { source: 'Training', enabled: false };
    const featureFlags: FeatureFlagsResponse = { Training: trainingFeatureFlags };

    const [{ registerModule }, { useNavigationStore }] = await Promise.all([
      import('@/modules/reports/ReportsModule'),
      import('@/stores/NavigationStore'),
    ]);

    registerModule(routes, featureFlags);

    const navigationStore = useNavigationStore();

    expect(routes).toHaveLength(2);
    expect(navigationStore.links).toHaveLength(0);
  });
});
