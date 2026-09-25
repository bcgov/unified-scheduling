import { beforeEach, describe, expect, it, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import { ref } from 'vue';

import { getGetApiUsersIdResponseMock } from '@/api-access/generated/users/users.msw';
import Identification from '@/modules/myteam/components/Identification.vue';
import { createTestApp } from '../../../helpers/createTestApp';

const { useTrainingProfileLookupMock } = vi.hoisted(() => ({
  useTrainingProfileLookupMock: vi.fn(),
}));

vi.mock('@/modules/training/trainingProfileApi', () => ({
  useTrainingProfileLookup: useTrainingProfileLookupMock,
}));

describe('Identification', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useTrainingProfileLookupMock.mockReturnValue({ data: ref([]) });
  });

  it('renders user identification details including badge when feature flag is enabled', async () => {
    const app = await createTestApp({
      featureFlags: { UserManagement: { enabled: true, userBadgeNumber: { enabled: true } } },
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
      featureFlags: { UserManagement: { enabled: true, userBadgeNumber: { enabled: false } } },
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

  it('shows training profile name when profile lookup contains matching id', async () => {
    const app = await createTestApp();
    useTrainingProfileLookupMock.mockReturnValue({
      data: ref([
        { id: 2, code: 'SUP', name: 'Supervisor' },
        { id: 4, code: 'OPS', name: 'Operations' },
      ]),
    });

    const user = {
      ...getGetApiUsersIdResponseMock(),
      trainingProfileId: 4,
    };

    const wrapper = mount(Identification, {
      props: {
        user,
      },
      global: {
        plugins: app.mountPlugins,
      },
    });

    expect(wrapper.text()).toContain('Training Profile');
    expect(wrapper.text()).toContain('Operations');
  });

  it('falls back to generated profile label when id is not in lookup', async () => {
    const app = await createTestApp();
    useTrainingProfileLookupMock.mockReturnValue({
      data: ref([{ id: 2, code: 'SUP', name: 'Supervisor' }]),
    });

    const user = {
      ...getGetApiUsersIdResponseMock(),
      trainingProfileId: 99,
    };

    const wrapper = mount(Identification, {
      props: {
        user,
      },
      global: {
        plugins: app.mountPlugins,
      },
    });

    expect(wrapper.text()).toContain('Profile 99');
  });
});
