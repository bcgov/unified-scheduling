import { describe, it, expect } from 'vitest';
import { nextTick } from 'vue';

import { mount } from '@vue/test-utils';

import App from '../App.vue';
import { createTestApp } from './helpers/createTestApp';
import { useConfigStore } from '@/stores/config';

describe('App', async () => {
  const app = await createTestApp();

  it('mounts renders properly', () => {
    const wrapper = mount(App, {
      global: {
        plugins: [app.router, app.vuetify],
      },
    });

    expect(wrapper.text()).toContain('Dashboard');
  });

  it('updates Vuetify theme colors when the selected brand changes', async () => {
    const testApp = await createTestApp();
    const configStore = useConfigStore(testApp.pinia);
    configStore.config = {
      ...configStore.config,
      selectedBrand: 'default',
      brandingOptions: {
        default: { primaryColor: '#123456', backgroundColor: '#eeeeee', navigationSelectedColor: '#abcdef' },
        bcss: { primaryColor: '#234075', backgroundColor: '#fefefe', navigationSelectedColor: '#fedcba' },
      },
    };
    testApp.vuetify.theme.themes.value.uaLight = testApp.vuetify.theme.themes.value.light;

    const wrapper = mount(App, {
      global: { plugins: testApp.mountPlugins },
    });
    await nextTick();

    const colors = testApp.vuetify.theme.themes.value.uaLight.colors;
    expect(colors.primary).toBe('#123456');
    expect(colors.background).toBe('#eeeeee');
    expect(colors.active).toBe('#abcdef');

    configStore.config.selectedBrand = 'bcss';
    await nextTick();

    expect(colors.primary).toBe('#234075');
    expect(colors.background).toBe('#fefefe');
    expect(colors.active).toBe('#fedcba');

    wrapper.unmount();
  });
});
