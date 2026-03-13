import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';

import { getGetApiUsersIdResponseMock } from '@/api-access/generated/users/users.msw';
import Identification from '@/modules/myteam/components/Identification.vue';

describe('Identification', () => {
  it('renders user identification details', () => {
    const user = getGetApiUsersIdResponseMock();

    const wrapper = mount(Identification, {
      props: {
        user,
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

    if (user.idirId) {
      expect(wrapper.text()).toContain(user.idirId);
    }

    if (user.email) {
      expect(wrapper.text()).toContain(user.email);
    }

    if (user.badgeNumber) {
      expect(wrapper.text()).toContain(user.badgeNumber);
    }
  });
});