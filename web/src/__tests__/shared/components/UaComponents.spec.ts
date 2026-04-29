import UaAlert from '@/shared/components/UaAlert.vue';
import UaCard from '@/shared/components/UaCard.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaPageHeader from '@/shared/components/UaPageHeader.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import { mount } from '@vue/test-utils';
import { beforeAll, describe, expect, it } from 'vitest';
import { createVuetify } from 'vuetify';
import { createTestApp } from '../../helpers/createTestApp';

describe('UaPageHeader', () => {
  it('renders title text', () => {
    const wrapper = mount(UaPageHeader, { props: { title: 'My Page' } });
    expect(wrapper.text()).toContain('My Page');
  });

  it('renders action slot when provided', () => {
    const wrapper = mount(UaPageHeader, {
      props: { title: 'My Page' },
      slots: { actions: '<button>Click</button>' },
    });
    expect(wrapper.text()).toContain('Click');
  });
});

describe('UaCard', () => {
  it('renders without header when no title', () => {
    const wrapper = mount(UaCard, { slots: { default: '<p>Body</p>' } });
    expect(wrapper.find('.ua-card__header').exists()).toBe(false);
    expect(wrapper.text()).toContain('Body');
  });

  it('renders header when title is provided', () => {
    const wrapper = mount(UaCard, {
      props: { title: 'Card Title' },
      slots: { default: '<p>Content</p>' },
    });
    expect(wrapper.find('.ua-card__header').exists()).toBe(true);
    expect(wrapper.text()).toContain('Card Title');
    expect(wrapper.text()).toContain('Content');
  });
});

describe('UaFormGrid', () => {
  it('renders slot content in a grid', () => {
    const wrapper = mount(UaFormGrid, {
      slots: { default: '<label>Name</label><input />' },
    });
    expect(wrapper.find('.ua-form-grid').exists()).toBe(true);
    expect(wrapper.text()).toContain('Name');
  });
});

// TODO: Previously used top-level `await createTestApp()` inside `async describe` which works
// with happy-dom but is not idiomatic. Use `beforeAll` for clarity and portability.
describe('UaTextField', () => {
  let vuetify: ReturnType<typeof createVuetify>;
  beforeAll(async () => {
    ({ vuetify } = await createTestApp());
  });

  it('renders label and input', () => {
    const wrapper = mount(UaTextField, {
      props: { label: 'Email', id: 'email-field' },
      global: { plugins: [vuetify] },
    });
    expect(wrapper.text()).toContain('Email');
    expect(wrapper.find('label.ua-form-label').attributes('for')).toBe('email-field');
  });
});

describe('UaAlert', () => {
  let vuetify: ReturnType<typeof createVuetify>;
  beforeAll(async () => {
    ({ vuetify } = await createTestApp());
  });

  it('renders alert content', () => {
    const wrapper = mount(UaAlert, {
      props: { type: 'error' },
      slots: { default: 'Something went wrong' },
      global: { plugins: [vuetify] },
    });
    expect(wrapper.text()).toContain('Something went wrong');
  });
});

describe('UaPlaceholderPage', () => {
  it('renders title and default description', () => {
    const wrapper = mount(UaPlaceholderPage, {
      props: { title: 'Coming Soon Feature' },
    });
    expect(wrapper.text()).toContain('Coming Soon Feature');
    expect(wrapper.text()).toContain('This feature is coming soon.');
  });

  it('renders custom description', () => {
    const wrapper = mount(UaPlaceholderPage, {
      props: { title: 'Search', description: 'Under construction' },
    });
    expect(wrapper.text()).toContain('Under construction');
  });
});

describe('UaModal', () => {
  let vuetify: ReturnType<typeof createVuetify>;
  beforeAll(async () => {
    ({ vuetify } = await createTestApp());
  });

  it('renders modal with title', () => {
    // v-dialog requires visualViewport which is unavailable in happy-dom.
    // We test the inner component structure by disabling teleport.
    const wrapper = mount(UaModal, {
      props: { title: 'Edit User' },
      slots: { default: '<p>Form content</p>' },
      global: {
        plugins: [vuetify],
        stubs: { VDialog: { template: '<div><slot /></div>' } },
      },
    });
    expect(wrapper.text()).toContain('Edit User');
    expect(wrapper.text()).toContain('Form content');
    expect(wrapper.find('.ua-modal__close-btn').exists()).toBe(true);
  });
});
