import { ref, computed } from 'vue';
import { defineStore } from 'pinia';

export const Modules = {
  scheduling: 'scheduling',
  users: 'users',
  training: 'training',
} as const;

export type ModuleKey = typeof Modules[keyof typeof Modules];

export interface FeatureModuleConfig {
  isEnabled?: boolean;
  description?: string;
}

export interface AppConfig {
  features?: {
    modules?: Record<ModuleKey, FeatureModuleConfig>;
  };
}

export const useConfigStore = defineStore('config', () => {
  const config = ref<AppConfig | null>(null);

  const isLoaded = computed(() => !!config.value);

  const loadConfig = async (): Promise<AppConfig | null> => {
    if (config.value) {
      return config.value;
    }

    const response = await fetch('/src/assets/config.json');
    config.value = await response.json();
    return config.value;
  };

  return {
    config,
    isLoaded,
    loadConfig,
  };
});
