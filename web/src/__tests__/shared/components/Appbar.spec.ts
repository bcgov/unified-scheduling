import { mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import Appbar from '@/shared/components/Appbar.vue';
import { useAuthStore } from '@/stores/auth';
import * as authNavigation from '@/utils/authNavigation';

describe('Appbar logout', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('navigates to backend logout without declaring the local user signed out', async () => {
    const app = await createTestApp({ isRegistered: false });
    await app.router.push('/unregistered');
    const authStore = useAuthStore(app.pinia);
    const userInfo = authStore.userInfo;
    const redirect = vi.spyOn(authNavigation, 'redirectToLogout').mockImplementation(() => {});
    const wrapper = mount(Appbar, {
      global: {
        plugins: app.mountPlugins,
        stubs: {
          VAppBar: { template: '<header><slot /></header>' },
          VMenu: { template: '<div><slot name="activator" :props="{}" /><slot /></div>' },
        },
      },
    });

    try {
      const logout = wrapper.findAll('button').find((button) => button.text().trim() === 'Logout');
      expect(logout).toBeDefined();
      await logout!.trigger('click');

      expect(redirect).toHaveBeenCalledOnce();
      expect(authStore.isAuthenticated).toBe(true);
      expect(authStore.userInfo).toBe(userInfo);
    } finally {
      wrapper.unmount();
      app.router.options.history.destroy();
    }
  });
});
