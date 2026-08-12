import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import type { RouteRecordRaw } from 'vue-router';
import type { FeatureFlagsResponse, TrainingFeatureFlags } from '@/api-access/generated/models';

describe('training module', () => {
  beforeEach(() => {
    vi.resetModules();
    setActivePinia(createPinia());
  });

  it('registers training route and navigation link when feature is enabled', async () => {
    const routes: RouteRecordRaw[] = [];
    const trainingFeatureFlags: TrainingFeatureFlags = { source: 'Training', enabled: true };
    const featureFlags: FeatureFlagsResponse = { Training: trainingFeatureFlags };

    const [{ registerModule }, { useNavigationStore }] = await Promise.all([
      import('@/modules/training/TrainingModule'),
      import('@/stores/NavigationStore'),
    ]);

    registerModule(routes, featureFlags);

    const navigationStore = useNavigationStore();

    expect(routes).toHaveLength(1);
    expect(routes[0]?.path).toBe('/training');
    expect(routes[0]?.meta).toMatchObject({ requiresAuth: true });
    expect(routes[0]?.children?.[0]?.name).toBe('Training');

    expect(navigationStore.links).toEqual([
      {
        name: 'Training',
        path: '/training',
        class: 'router-link--border',
      },
    ]);
  });

  it('does not register training routes when feature is disabled', async () => {
    const routes: RouteRecordRaw[] = [];
    const trainingFeatureFlags: TrainingFeatureFlags = { source: 'Training', enabled: false };
    const featureFlags: FeatureFlagsResponse = { Training: trainingFeatureFlags };

    const [{ registerModule }, { useNavigationStore }] = await Promise.all([
      import('@/modules/training/TrainingModule'),
      import('@/stores/NavigationStore'),
    ]);

    registerModule(routes, featureFlags);

    const navigationStore = useNavigationStore();

    expect(routes).toHaveLength(0);
    expect(navigationStore.links).toHaveLength(0);
  });
});
