import { ref, computed } from 'vue';
import { defineStore } from 'pinia';
import { getApiConfig } from '@/api-access/generated/config/config';
import type { ConfigResponse } from '@/api-access/generated/models';

export const useConfigStore = defineStore('config', () => {
  const config = ref<ConfigResponse | null>(null);

  const isLoaded = computed(() => !!config.value);

  const loadConfig = async (): Promise<ConfigResponse | null> => {
    if (config.value) {
      return config.value;
    }

    const { data } = await getApiConfig();
    config.value = data.value;
    return config.value;
  };

  return {
    config,
    isLoaded,
    loadConfig,
  };
});
