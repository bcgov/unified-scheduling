import UaDataTable from '@/shared/components/UaDataTable.vue';
import { mount } from '@vue/test-utils';
import { defineComponent } from 'vue';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { useDraggableMock } = vi.hoisted(() => ({
  useDraggableMock: vi.fn(),
}));

vi.mock('vue-draggable-plus', () => ({
  useDraggable: useDraggableMock,
}));

const VDataTableStub = defineComponent({
  name: 'VDataTable',
  props: {
    itemsPerPage: { type: Number, required: false },
    page: { type: [Number, String], required: false },
    searchPlaceholder: { type: String, required: false },
  },
  template: '<div class="v-data-table-stub"><slot /></div>',
});

describe('UaDataTable', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useDraggableMock.mockReturnValue({ destroy: vi.fn() });
  });

  it('forwards items-per-page to the underlying data table', () => {
    const wrapper = mount(UaDataTable, {
      props: {
        items: [{ id: 1, name: 'Item 1' }],
        paginate: false,
      },
      attrs: {
        'items-per-page': 7,
      },
      global: {
        stubs: {
          VDataTable: VDataTableStub,
          VTextField: true,
        },
      },
    });

    const dataTable = wrapper.findComponent(VDataTableStub);

    expect(dataTable.exists()).toBe(true);
    expect(dataTable.props('itemsPerPage')).toBe(7);
  });

  it('does not forward excluded attrs', () => {
    const wrapper = mount(UaDataTable, {
      props: {
        items: [{ id: 1, name: 'Item 1' }],
      },
      attrs: {
        page: 2,
        'search-placeholder': 'Search users',
      },
      global: {
        stubs: {
          VDataTable: VDataTableStub,
          VTextField: true,
        },
      },
    });

    const dataTable = wrapper.findComponent(VDataTableStub);

    expect(dataTable.exists()).toBe(true);
    expect(dataTable.props('page')).toBeUndefined();
    expect(dataTable.props('searchPlaceholder')).toBeUndefined();
  });
});
