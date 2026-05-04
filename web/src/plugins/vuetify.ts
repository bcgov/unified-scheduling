import { createVuetify, type ThemeDefinition } from 'vuetify';
import { aliases, mdi } from 'vuetify/iconsets/mdi-svg';
import 'vuetify/styles';

const uaLightTheme: ThemeDefinition = {
  dark: false,
  colors: {
    primary: '#013366',
    secondary: '#1E5189',
    accent: '#FCBA19',
    success: '#42814A',
    error: '#CE3E39',
    warning: '#F8BB47',
    info: '#255A90',
    surface: '#FFFFFF',
    'surface-light': '#FAF9F8',
    'surface-variant': '#ECEAE8',
    background: '#FAF9F8',
    'on-primary': '#FFFFFF',
    'on-secondary': '#FFFFFF',
    'on-surface': '#2D2D2D',
    'on-background': '#2D2D2D',
    'header-green': '#42814A',
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
