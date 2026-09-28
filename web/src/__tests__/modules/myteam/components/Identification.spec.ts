import { beforeEach, describe, expect, it, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import { ref } from 'vue';

import { getGetApiUsersIdResponseMock } from '@/api-access/generated/users/users.msw';
import type { Permissions } from '@/api-access/generated/models';
import Identification from '@/modules/myteam/components/Identification.vue';
import { createTestApp } from '../../../helpers/createTestApp';

const { getApiTrainingProfileTypesMock } = vi.hoisted(() => ({
  getApiTrainingProfileTypesMock: vi.fn(),
}));

vi.mock('@/api-access/generated/training/training', () => ({
  getApiTrainingProfileTypes: getApiTrainingProfileTypesMock,
}));

describe('Identification', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getApiTrainingProfileTypesMock.mockReturnValue({
      data: ref([]),
      isFetching: ref(false),
      execute: vi.fn().mockResolvedValue(undefined),
    });
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
    const app = await createTestApp({ permissions: ['TrainingsView' as unknown as Permissions] });
    getApiTrainingProfileTypesMock.mockReturnValue({
      data: ref([
        { id: 2, code: 'SUP', name: 'Supervisor' },
        { id: 4, code: 'OPS', name: 'Operations' },
      ]),
      isFetching: ref(false),
      execute: vi.fn().mockResolvedValue(undefined),
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

  it('shows unavailable text when profile id is not in lookup', async () => {
    const app = await createTestApp({ permissions: ['TrainingsView' as unknown as Permissions] });
    getApiTrainingProfileTypesMock.mockReturnValue({
      data: ref([{ id: 2, code: 'SUP', name: 'Supervisor' }]),
      isFetching: ref(false),
      execute: vi.fn().mockResolvedValue(undefined),
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

    expect(wrapper.text()).toContain('Training profile unavailable');
  });

  it('shows loading text while profile lookup is in progress', async () => {
    const app = await createTestApp({ permissions: ['TrainingsView' as unknown as Permissions] });
    getApiTrainingProfileTypesMock.mockReturnValue({
      data: ref([]),
      isFetching: ref(true),
      execute: vi.fn().mockResolvedValue(undefined),
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

    expect(wrapper.text()).toContain('Loading training profile...');
  });

  it('shows placeholder when user lacks TrainingsView permission', async () => {
    const app = await createTestApp({ permissions: [] });
    getApiTrainingProfileTypesMock.mockReturnValue({
      data: ref([{ id: 4, code: 'OPS', name: 'Operations' }]),
      isFetching: ref(false),
      execute: vi.fn().mockResolvedValue(undefined),
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
    expect(wrapper.text()).not.toContain('Operations');
    expect(wrapper.text()).toContain('-');
  });
});
