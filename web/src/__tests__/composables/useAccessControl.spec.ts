import { createPinia } from 'pinia';
import { describe, expect, it } from 'vitest';
import { getGetApiConfigResponseMock } from '@/api-access/generated/config/config.msw';
import { useAccessControl } from '@/composables/useAccessControl';

const config = getGetApiConfigResponseMock();

describe('useAccessControl', () => {
  it('returns true when the requested feature flag is enabled', () => {
    const pinia = createPinia();
    const { configStore, isFeatureFlagEnabled } = useAccessControl(pinia);

    config.featureFlags.schedulingModule = true;
    configStore.config = config;

    expect(isFeatureFlagEnabled('schedulingModule')).toBe(true);
  });

  it('returns false when the requested feature flag is disabled', () => {
    const pinia = createPinia();
    const { configStore, isFeatureFlagEnabled } = useAccessControl(pinia);

    config.featureFlags.schedulingModule = false;
    configStore.config = config;

    expect(isFeatureFlagEnabled('schedulingModule')).toBe(false);
  });

  it('returns false when config is not loaded', () => {
    const pinia = createPinia();
    const { configStore, isFeatureFlagEnabled } = useAccessControl(pinia);

    configStore.config = null;

    expect(isFeatureFlagEnabled('schedulingModule')).toBe(false);
  });
});
