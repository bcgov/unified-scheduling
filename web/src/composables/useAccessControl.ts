import { computed } from 'vue';
import type { Pinia } from 'pinia';
import { useConfigStore, type FeatureModuleConfig, type ModuleKey } from '@/stores/config';

export const useAccessControl = (pinia?: Pinia) => {
  const configStore = useConfigStore(pinia);

  const modules = computed(
    () => configStore.config?.features?.modules ?? ({} as Partial<Record<ModuleKey, FeatureModuleConfig>>),
  );

  const canAccessModule = (moduleKey: ModuleKey): boolean => {
    return modules.value[moduleKey]?.isEnabled ?? false;
  };

  return {
    configStore,
    modules,
    canAccessModule,
  };
};
