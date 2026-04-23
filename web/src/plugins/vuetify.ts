import 'vuetify/styles';
import { createVuetify, type ThemeDefinition } from 'vuetify';
import { aliases, mdi } from 'vuetify/iconsets/mdi';

const uaLightTheme: ThemeDefinition = {
  dark: false,
  colors: {
    primary: '#003366',
    secondary: '#38598A',
    accent: '#FCBA19',
    success: '#2E8540',
    error: '#D8292F',
    warning: '#F5A623',
    info: '#1A5A96',
    surface: '#FFFFFF',
    'surface-light': '#F2F2F2',
    'surface-variant': '#E9E9EB',
    background: '#F2F2F2',
    'on-primary': '#FFFFFF',
    'on-secondary': '#FFFFFF',
    'on-surface': '#313132',
    'on-background': '#313132',
    'header-green': '#2E8540',
  },
};

export default createVuetify({
  theme: {
    defaultTheme: 'uaLight',
    themes: {
      uaLight: uaLightTheme,
    },
  },
  icons: {
    defaultSet: 'mdi',
    aliases,
    sets: {
      mdi,
    },
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
  },
});
