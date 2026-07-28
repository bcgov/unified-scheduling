import UserTrainingView from '@/modules/myteam/views/UserTrainingView.vue';
import { mount } from '@vue/test-utils';
import { defineComponent, ref } from 'vue';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const {
  getApiTrainingsUsersUserIdMock,
  getApiLookupTrainingsMock,
  hasPermissionMock,
} = vi.hoisted(() => ({
  getApiTrainingsUsersUserIdMock: vi.fn(),
  getApiLookupTrainingsMock: vi.fn(),
  hasPermissionMock: vi.fn(() => true),
}));

vi.mock('@/api-access/generated/user-training/user-training', () => ({
  getApiTrainingsUsersUserId: getApiTrainingsUsersUserIdMock,
}));

vi.mock('@/api-access/generated/training/training', () => ({
  getApiLookupTrainings: getApiLookupTrainingsMock,
}));

vi.mock('@/composables/useAccessControl', () => ({
  useAccessControl: () => ({
    hasPermission: hasPermissionMock,
  }),
}));

vi.mock('@/api-access/generated/models', () => ({
  Permissions: {
    UserTrainingsView: 'UserTrainingsView',
    UserTrainingsCreate: 'UserTrainingsCreate',
    UserTrainingsEdit: 'UserTrainingsEdit',
    UserTrainingsDelete: 'UserTrainingsDelete',
  },
}));

const UaDataTableStub = defineComponent({
  name: 'UaDataTable',
  props: {
    items: {
      type: Array,
      required: false,
      default: () => [],
    },
  },
  template: '<div class="ua-data-table-stub">rows: {{ items.length }}</div>',
});

describe('UserTrainingView', () => {
  beforeEach(() => {
    vi.clearAllMocks();

    getApiTrainingsUsersUserIdMock.mockReturnValue({
      data: ref([
        {
          id: 10,
          userId: '95f91fd1-1111-2222-3333-9c0aeb4ca44b',
          trainingId: 1,
          version: 1,
          trainingCode: 'CPR',
          trainingCategoryName: 'Medical',
          awardedOn: '2026-01-01T00:00:00Z',
          expiryDate: '2026-01-10T00:00:00Z',
          noticeState: 'None',
          notes: 'older',
          createdOn: '2026-01-01T00:00:00Z',
          updatedOn: null,
        },
        {
          id: 11,
          userId: '95f91fd1-1111-2222-3333-9c0aeb4ca44b',
          trainingId: 1,
          version: 2,
          trainingCode: 'CPR',
          trainingCategoryName: 'Medical',
          awardedOn: '2026-02-01T00:00:00Z',
          expiryDate: '2026-02-10T00:00:00Z',
          noticeState: 'None',
          notes: 'latest',
          createdOn: '2026-02-01T00:00:00Z',
          updatedOn: null,
        },
        {
          id: 12,
          userId: '95f91fd1-1111-2222-3333-9c0aeb4ca44b',
          trainingId: 2,
          version: 1,
          trainingCode: 'FA',
          trainingCategoryName: 'Safety',
          awardedOn: '2026-02-05T00:00:00Z',
          expiryDate: null,
          noticeState: 'None',
          notes: null,
          createdOn: '2026-02-05T00:00:00Z',
          updatedOn: null,
        },
      ]),
      error: ref(null),
      isFetching: ref(false),
      execute: vi.fn(),
    });

    getApiLookupTrainingsMock.mockReturnValue({
      data: ref([
        { id: 1, code: 'CPR', description: 'CPR' },
        { id: 2, code: 'FA', description: 'First Aid' },
      ]),
      error: ref(null),
      isFetching: ref(false),
      execute: vi.fn(),
    });
  });

  it('shows latest version per training in the table', () => {
    const wrapper = mount(UserTrainingView, {
      props: {
        user: {
          id: '95f91fd1-1111-2222-3333-9c0aeb4ca44b',
          idirName: 'tester',
          isEnabled: true,
          firstName: 'Test',
          lastName: 'User',
          email: 'test.user@example.com',
          gender: 'Other',
        },
      },
      global: {
        stubs: {
          UaDataTable: UaDataTableStub,
          UaAlert: true,
          UaBtn: true,
          UaPlaceholderPage: true,
          DeleteUserTrainingModal: true,
          UserTrainingModal: true,
          UserTrainingVersionsModal: true,
          VIcon: true,
        },
      },
    });

    const table = wrapper.findComponent(UaDataTableStub);
    expect(table.exists()).toBe(true);

    const items = table.props('items') as Array<{ trainingId: number; version: number }>;
    expect(items).toHaveLength(2);

    const cpr = items.find((item) => item.trainingId === 1);
    expect(cpr?.version).toBe(2);
  });
});
