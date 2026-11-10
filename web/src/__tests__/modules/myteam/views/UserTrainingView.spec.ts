import UserTrainingView from '@/modules/myteam/views/UserTrainingView.vue';
import type { UserResponse } from '@/api-access/generated/models';
import { mount } from '@vue/test-utils';
import { defineComponent, ref } from 'vue';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { useLocationsStore } from '@/stores/LocationsStore';

const { getApiTrainingsUsersUserIdMock, getApiLookupTrainingsMock, hasPermissionMock } = vi.hoisted(() => ({
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

const UaDataTableStub = defineComponent({
  name: 'UaDataTable',
  props: {
    items: {
      type: Array,
      required: false,
      default: () => [],
    },
  },
  template: `
    <div class="ua-data-table-stub">
      <div class="ua-data-table-stub__count">rows: {{ items.length }}</div>
      <div v-for="item in items" :key="item.id" class="ua-data-table-stub__row">
        <slot name="item.awardedOn" :item="item" />
        <slot name="item.endingOn" :item="item" />
        <slot name="item.expiryDate" :item="item" />
        <slot name="item.actions" :item="item" />
      </div>
    </div>
  `,
});

const makeUser = (): UserResponse =>
  ({
    id: '95f91fd1-1111-2222-3333-9c0aeb4ca44b',
    idirName: 'tester',
    isEnabled: true,
    firstName: 'Test',
    lastName: 'User',
    email: 'test.user@example.com',
    gender: 'Other',
    employeeNumber: '12345',
    homeLocationId: 1,
  }) as unknown as UserResponse;

describe('UserTrainingView', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const app = await createTestApp({ loadConfig: false });
    useLocationsStore(app.pinia).entities = [{ id: 1, name: 'Vancouver', timezone: 'America/Vancouver' }];

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
          endingOn: '2026-01-01T00:00:00Z',
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
          endingOn: '2026-02-01T00:00:00Z',
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
          endingOn: '2026-02-05T00:00:00Z',
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
        { id: 1, code: 'CPR', description: 'CPR', rotating: true },
        { id: 2, code: 'FA', description: 'First Aid', rotating: false },
      ]),
      error: ref(null),
      isFetching: ref(false),
      execute: vi.fn(),
    });
  });

  it('shows latest version per training in the table', () => {
    const wrapper = mount(UserTrainingView, {
      props: {
        user: makeUser(),
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
    expect(table.text()).toContain('2026-01-31 4:00 PM');
  });

  it('shows renew action only for rotating trainings', () => {
    const wrapper = mount(UserTrainingView, {
      props: {
        user: makeUser(),
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

    const renewButtons = wrapper.findAll('ua-btn-stub[title="Renew training record"]');
    expect(renewButtons).toHaveLength(1);
  });

  it('uses the training user home timezone for display and the edit modal', async () => {
    useLocationsStore().entities.push({ id: 2, name: 'Edmonton', timezone: 'America/Edmonton' });
    getApiTrainingsUsersUserIdMock.mockReturnValueOnce({
      data: ref([
        {
          id: 20,
          trainingId: 1,
          allDay: false,
          awardedOn: '2026-07-20T22:00:00Z',
          endingOn: '2026-07-21T01:00:00Z',
          expiryDate: '2026-08-20T05:59:59.999Z',
        },
      ]),
      error: ref(null),
      isFetching: ref(false),
      execute: vi.fn(),
    });
    const wrapper = mount(UserTrainingView, {
      props: { user: { ...makeUser(), homeLocationId: 2 } },
      global: {
        stubs: {
          UaDataTable: UaDataTableStub,
          UaAlert: true,
          UaBtn: true,
          UaPlaceholderPage: true,
          UserTrainingModal: true,
          UserTrainingVersionsModal: true,
          VIcon: true,
        },
      },
    });
    const table = wrapper.getComponent(UaDataTableStub);
    expect(table.text()).toContain('2026-07-20 4:00 PM');
    expect(table.text()).toContain('2026-07-20 7:00 PM');
    expect(table.text()).toContain('2026-08-19');
    await wrapper.get('ua-btn-stub[title="Edit training record"]').trigger('click');
    expect(wrapper.findComponent({ name: 'UserTrainingModal' }).props('timeZone')).toBe('America/Edmonton');
  });

  it('keeps all-day calendar dates when the user has no home location', () => {
    getApiTrainingsUsersUserIdMock.mockReturnValueOnce({
      data: ref([
        {
          id: 20,
          trainingId: 1,
          allDay: true,
          awardedOn: '2026-07-20T00:00:00Z',
          endingOn: '2026-07-22T00:00:00Z',
          expiryDate: null,
        },
      ]),
      error: ref(null),
      isFetching: ref(false),
      execute: vi.fn(),
    });
    const wrapper = mount(UserTrainingView, {
      props: { user: { ...makeUser(), homeLocationId: null } },
      global: {
        stubs: { UaDataTable: UaDataTableStub, UaAlert: true, UaBtn: true, UaPlaceholderPage: true, VIcon: true },
      },
    });
    expect(wrapper.getComponent(UaDataTableStub).text()).toContain('2026-07-20');
    expect(wrapper.getComponent(UaDataTableStub).text()).toContain('2026-07-22');
  });

  it('shows all-day calendar dates without shifting to the preceding Vancouver day', () => {
    getApiTrainingsUsersUserIdMock.mockReturnValueOnce({
      data: ref([
        {
          id: 20,
          userId: makeUser().id,
          trainingId: 1,
          version: 1,
          trainingCode: 'CPR',
          allDay: true,
          awardedOn: '2026-07-20T00:00:00Z',
          endingOn: '2026-07-22T00:00:00Z',
          expiryDate: '2026-07-24T06:59:59.999Z',
        },
      ]),
      error: ref(null),
      isFetching: ref(false),
      execute: vi.fn(),
    });
    const wrapper = mount(UserTrainingView, {
      props: { user: makeUser() },
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

    const table = wrapper.getComponent(UaDataTableStub);
    expect(table.text()).toContain('2026-07-20');
    expect(table.text()).toContain('2026-07-22');
    expect(table.text()).toContain('2026-07-23');
    expect(table.text()).not.toContain('2026-07-19');
  });
});
