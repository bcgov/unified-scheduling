import UaDataTableServer from '@/shared/components/UaDataTableServer.vue';
import { mount } from '@vue/test-utils';
import { beforeAll, describe, expect, it } from 'vitest';
import { createVuetify } from 'vuetify';
import { VDataTableServer } from 'vuetify/components';
import { createTestApp } from '../../helpers/createTestApp';

describe('UaDataTableServer', () => {
  let vuetify: ReturnType<typeof createVuetify>;

  beforeAll(async () => {
    ({ vuetify } = await createTestApp());
  });

  it('renders wrapper and v-data-table-server with default props', () => {
    const wrapper = mount(UaDataTableServer, {
      props: {
        itemsLength: 0,
      },
      global: {
        plugins: [vuetify],
      },
    });

    expect(wrapper.find('.ua-data-table-wrapper').exists()).toBe(true);
    expect(wrapper.findComponent(VDataTableServer).exists()).toBe(true);
    expect(wrapper.findComponent(VDataTableServer).props('items')).toEqual([]);
    expect(wrapper.findComponent(VDataTableServer).props('itemsLength')).toBe(0);
    expect(wrapper.findComponent(VDataTableServer).props('loading')).toBe(false);
  });

  it('passes items, itemsLength, and loading props to VDataTableServer', () => {
    const items = [
      { id: 1, name: 'Alice', role: 'Admin' },
      { id: 2, name: 'Bob', role: 'User' },
    ];

    const wrapper = mount(UaDataTableServer, {
      props: {
        items,
        itemsLength: 50,
        loading: true,
      },
      global: {
        plugins: [vuetify],
      },
    });

    const tableServer = wrapper.findComponent(VDataTableServer);
    expect(tableServer.props('items')).toEqual(items);
    expect(tableServer.props('itemsLength')).toBe(50);
    expect(tableServer.props('loading')).toBe(true);
  });

  it('forwards custom attributes and pagination props to VDataTableServer', () => {
    const headers = [
      { title: 'ID', key: 'id' },
      { title: 'Name', key: 'name' },
    ];

    const wrapper = mount(UaDataTableServer, {
      props: {
        itemsLength: 100,
      },
      attrs: {
        headers,
        page: 2,
        'items-per-page': 10,
        density: 'compact',
      },
      global: {
        plugins: [vuetify],
      },
    });

    const tableServer = wrapper.findComponent(VDataTableServer);
    expect(tableServer.props('headers')).toEqual(headers);
    expect(tableServer.props('page')).toBe(2);
    expect(tableServer.props('itemsPerPage')).toBe(10);
    expect(tableServer.props('density')).toBe('compact');
  });

  it('forwards custom slots to VDataTableServer', () => {
    const items = [{ id: 1, name: 'Alice' }];
    const headers = [
      { title: 'ID', key: 'id' },
      { title: 'Name', key: 'name' },
      { title: 'Actions', key: 'actions' },
    ];

    const wrapper = mount(UaDataTableServer, {
      props: {
        items,
        itemsLength: 1,
      },
      attrs: {
        headers,
      },
      slots: {
        top: '<div class="custom-top-slot">Header Content</div>',
        'item.actions': '<button class="custom-action-btn">Action</button>',
        noData: '<div class="custom-no-data">No records available</div>',
      },
      global: {
        plugins: [vuetify],
      },
    });

    expect(wrapper.find('.custom-top-slot').exists()).toBe(true);
    expect(wrapper.find('.custom-top-slot').text()).toBe('Header Content');
    expect(wrapper.find('.custom-action-btn').exists()).toBe(true);
  });

  it('emits update events from VDataTableServer when pagination changes', async () => {
    const wrapper = mount(UaDataTableServer, {
      props: {
        itemsLength: 100,
      },
      attrs: {
        page: 1,
        'items-per-page': 10,
      },
      global: {
        plugins: [vuetify],
      },
    });

    const tableServer = wrapper.findComponent(VDataTableServer);
    await tableServer.vm.$emit('update:page', 2);
    await tableServer.vm.$emit('update:itemsPerPage', 25);
    await tableServer.vm.$emit('update:options', { page: 2, itemsPerPage: 25, sortBy: [] });

    expect(wrapper.emitted('update:page')?.[0]).toEqual([2]);
    expect(wrapper.emitted('update:itemsPerPage')?.[0]).toEqual([25]);
    expect(wrapper.emitted('update:options')?.[0]).toEqual([{ page: 2, itemsPerPage: 25, sortBy: [] }]);
  });
});
