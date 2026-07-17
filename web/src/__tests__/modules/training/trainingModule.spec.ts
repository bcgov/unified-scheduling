import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import type { RouteRecordRaw } from 'vue-router';

describe('training module', () => {
  beforeEach(() => {
    vi.resetModules();
    setActivePinia(createPinia());
  });

  it('registers training route and navigation link', async () => {
    const routes: RouteRecordRaw[] = [];

    const [{ registerModule }, { useNavigationStore }] = await Promise.all([
      import('@/modules/training/TrainingModule'),
      import('@/stores/NavigationStore'),
    ]);

    registerModule(routes);

    const navigationStore = useNavigationStore();

    expect(routes).toHaveLength(1);
    expect(routes[0]?.path).toBe('/training');
    expect(routes[0]?.meta).toMatchObject({ module: 'trainingModule', requiresAuth: true });
    expect(routes[0]?.children?.[0]?.name).toBe('Training');

    expect(navigationStore.links).toEqual([
      {
        name: 'Training',
        path: '/training',
        class: 'router-link--border',
      },
    ]);
  });
});
