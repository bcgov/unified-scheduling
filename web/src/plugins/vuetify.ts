import { createVuetify, type ThemeDefinition } from 'vuetify';
import LuxonAdapter from '@date-io/luxon';
import { aliases, mdi } from 'vuetify/iconsets/mdi-svg';
import 'vuetify/styles';
import { defaultBrand } from '@/branding/brandingConfig';
import type { BrandDefinition } from '@/branding/types';

const uaLightTheme: ThemeDefinition = {
  dark: false,
  colors: {
    primary: defaultBrand.primaryColor,
    active: defaultBrand.activeColor,
    secondary: '#1E5189',
    accent: '#FCBA19',
    success: '#42814A',
    error: '#CE3E39',
    warning: '#F8BB47',
    info: '#255A90',
    surface: '#FFFFFF',
    'surface-light': '#FAF9F8',
    'surface-variant': '#ECEAE8',
    background: defaultBrand.backgroundColor,
    'on-primary': '#FFFFFF',
    'on-secondary': '#FFFFFF',
    'on-surface': '#2D2D2D',
    'on-background': '#2D2D2D',
    'header-green': '#42814A',
  },
};

export const createAppVuetify = (branding: BrandDefinition = defaultBrand) =>
  createVuetify({
    theme: {
      defaultTheme: 'uaLight',
      themes: {
        uaLight: {
          ...uaLightTheme,
          colors: {
            ...uaLightTheme.colors,
            primary: branding.primaryColor,
            background: branding.backgroundColor,
            active: branding.primaryColor,
          },
        },
      },
    },
    icons: {
      defaultSet: 'mdi',
      aliases,
      sets: {
        mdi,
      },
    },
    date: {
      adapter: LuxonAdapter,
    },
    defaults: {
      global: {
        ripple: false,
      },
      VTextField: {
        density: 'compact',
        variant: 'outlined',
      },
      VSelect: {
        itemColor: '#000000',
        density: 'compact',
        variant: 'outlined',
      },
      VTextarea: {
        density: 'compact',
        variant: 'outlined',
      },
      VBtn: {
        style: 'text-transform: none; letter-spacing: 0;',
      },
      VListItem: {
        color: 'active',
      },
      VTabs: {
        color: 'active',
      },
    },
  });
