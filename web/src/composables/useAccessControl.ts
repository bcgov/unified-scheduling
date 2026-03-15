import type { Pinia } from 'pinia';
import { useConfigStore } from '@/stores/config';

export const useAccessControl = (pinia?: Pinia) => {
  const configStore = useConfigStore(pinia);

  const isFeatureFlagEnabled = (moduleKey: string): boolean => {
    if (moduleKey === 'myteamsModule' && configStore.config?.featureFlags?.myTeamsModule) {
      return true;
    }

    if (moduleKey === 'schedulingModule' && configStore.config?.featureFlags?.schedulingModule) {
      return true;
    }

    if (moduleKey === 'userBadgeNumber' && configStore.config?.featureFlags?.userBadgeNumber) {
      return true;
    }

    return false;
  };

  return {
    configStore,
    isFeatureFlagEnabled: isFeatureFlagEnabled,
  };
};
