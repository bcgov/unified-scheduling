import { beforeEach, describe, expect, it } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import type { RouteRecordRaw } from 'vue-router';
import { useNavigationStore } from '@/stores/NavigationStore';
import { registerModule } from '@/modules/scheduling/SchedulingModule';

describe('scheduling module integration', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it('registers its route and navigation when enabled', () => {
    const routes: RouteRecordRaw[] = [];

    registerModule(routes, { Scheduling: { enabled: true } });

    expect(routes).toHaveLength(1);
    expect(routes[0]?.path).toBe('/schedule');
    expect(useNavigationStore().links).toEqual([
      { name: 'Schedule', path: '/schedule', class: 'router-link--border' },
    ]);
  });

  it('does not register when disabled', () => {
    const routes: RouteRecordRaw[] = [];

    registerModule(routes, { Scheduling: { enabled: false } });

    expect(routes).toHaveLength(0);
    expect(useNavigationStore().links).toHaveLength(0);
  });
});
