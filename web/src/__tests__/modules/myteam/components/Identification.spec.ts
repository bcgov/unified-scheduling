import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';

import { getGetApiUsersIdResponseMock } from '@/api-access/generated/users/users.msw';
import Identification from '@/modules/myteam/components/Identification.vue';
import { createTestApp } from '../../../helpers/createTestApp';

describe('Identification', () => {
  it('renders user identification details including badge when feature flag is enabled', async () => {
    const app = await createTestApp({
      featureFlags: {
        userBadgeNumber: true,
      },
    });

    const user = getGetApiUsersIdResponseMock();

    const wrapper = mount(Identification, {
      props: {
        user,
      },
      global: {
        plugins: app.mountPlugins,
      },
    });

    expect(wrapper.text()).toContain('Identification');
    expect(wrapper.text()).toContain('First Name');
    expect(wrapper.text()).toContain('Last Name');

    if (user.firstName) {
      expect(wrapper.text()).toContain(user.firstName);
    }

    if (user.lastName) {
      expect(wrapper.text()).toContain(user.lastName);
    }

    if (user.idirName) {
      expect(wrapper.text()).toContain(user.idirName);
    }

    if (user.email) {
      expect(wrapper.text()).toContain(user.email);
    }

    if (user.badgeNumber) {
      expect(wrapper.text()).toContain(user.badgeNumber);
    }
  });

  it('hides badge number when feature flag is disabled', async () => {
    const app = await createTestApp({
      featureFlags: {
        userBadgeNumber: false,
      },
    });

    const user = getGetApiUsersIdResponseMock({
      badgeNumber: 'ABC123',
    });

    const wrapper = mount(Identification, {
      props: {
        user,
      },
      global: {
        plugins: app.mountPlugins,
      },
    });

    expect(wrapper.text()).not.toContain('Badge Number');
    expect(wrapper.text()).not.toContain('ABC123');
  });
});
