import { mount } from '@vue/test-utils';
import { nextTick } from 'vue';
import { describe, expect, it } from 'vitest';
import Dashboard from '@/modules/dashboard/Dashboard.vue';
import { useAuthStore } from '@/stores/auth';
import { createTestApp } from '@/__tests__/helpers/createTestApp';

const featureFlags = {
  UserManagement: { source: 'UserManagement', enabled: true },
  Training: { source: 'Training', enabled: true },
};

describe('Dashboard', () => {
  it('links personal tiles to the signed-in user using the named routes', async () => {
    const app = await createTestApp({ featureFlags });
    const authStore = useAuthStore(app.pinia);
    authStore.setUserInfo({ ...authStore.userInfo!, userId: '42' });

    const wrapper = mount(Dashboard, { global: { plugins: app.mountPlugins } });

    expect(wrapper.find('[data-tile="My Profile"]').element.closest('a')?.getAttribute('href')).toBe('/myteam/42');
    expect(wrapper.find('[data-tile="My Training"]').element.closest('a')?.getAttribute('href')).toBe(
      '/myteam/42/training',
    );

    authStore.setUserInfo({ ...authStore.userInfo!, userId: '99' });
    await nextTick();

    expect(wrapper.find('[data-tile="My Profile"]').element.closest('a')?.getAttribute('href')).toBe('/myteam/99');
    expect(wrapper.find('[data-tile="My Training"]').element.closest('a')?.getAttribute('href')).toBe(
      '/myteam/99/training',
    );
  });

  it('does not render personal routes when the signed-in user ID is unavailable', async () => {
    const app = await createTestApp({ featureFlags });
    const wrapper = mount(Dashboard, { global: { plugins: app.mountPlugins } });

    expect(wrapper.find('[data-tile="My Profile"]').exists()).toBe(false);
    expect(wrapper.find('[data-tile="My Training"]').exists()).toBe(false);
    expect(wrapper.find('[data-tile="My Team"]').exists()).toBe(true);
  });

  it('does not render personal routes when user management is disabled', async () => {
    const app = await createTestApp({
      featureFlags: { ...featureFlags, UserManagement: { source: 'UserManagement', enabled: false } },
    });
    const authStore = useAuthStore(app.pinia);
    authStore.setUserInfo({ ...authStore.userInfo!, userId: '42' });
    const wrapper = mount(Dashboard, { global: { plugins: app.mountPlugins } });

    expect(wrapper.find('[data-tile="My Profile"]').exists()).toBe(false);
    expect(wrapper.find('[data-tile="My Training"]').exists()).toBe(false);
  });
});
