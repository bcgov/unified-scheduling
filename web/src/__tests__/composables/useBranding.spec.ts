import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it } from 'vitest';
import { useBranding } from '@/composables/useBranding';
import { defaultBrand } from '@/branding';
import { useAuthStore } from '@/stores/auth';
import { useConfigStore } from '@/stores/config';
import type { BrandSettingsOptions } from '@/api-access/generated/models';

const brandingOptions: Record<string, BrandSettingsOptions> = {
  default: {
    applicationName: 'Default operations',
    primaryColor: '#123456',
    backgroundColor: '#eeeeee',
    navigationSelectedColor: '#abcdef',
    quickLinks: ['myProfile', 'training'],
    dashboardMessage: 'Default message',
  },
  BCSS: {
    applicationName: 'BC Sheriff Service',
    primaryColor: '#654321',
    backgroundColor: '#dddddd',
    navigationSelectedColor: '#fedcba',
    quickLinks: ['schedule', 'myTeam'],
    dashboardMessage: 'BCSS message',
    secondaryLogoUrl: '/images/bcss-logo.svg',
    secondaryLogoAlt: 'BC Sheriff Service logo',
    backgroundImageUrl: '/images/bcss-overlay.svg',
    header: { title: 'BCSS dashboard', subtitle: 'Sheriff operations' },
  },
};

function authenticate() {
  useAuthStore().userInfo = {
    isAuthenticated: true,
    isRegistered: true,
    name: 'Test User',
    authenticationType: 'test',
    claims: [],
    permissions: [],
    userId: null,
    homeLocationId: null,
  };
}

describe('useBranding', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it('uses bundled defaults before configuration loads, including for authenticated users', () => {
    const branding = useBranding();

    expect(branding.applicationName.value).toBe(defaultBrand.applicationName);
    expect(branding.primaryColor.value).toBe(defaultBrand.primaryColor);
    expect(branding.backgroundColor.value).toBe(defaultBrand.backgroundColor);
    expect(branding.activeColor.value).toBe(defaultBrand.activeColor);
    expect(branding.dashboardTiles.value).toEqual(defaultBrand.dashboardTiles);
    expect(branding.dashboardMessage.value).toBe(defaultBrand.dashboardMessage);
    expect(branding.dashboardBanner.value).toEqual(defaultBrand.dashboardBanner);
    expect(branding.primaryLogoUrl.value).toBe('/images/bcid-logo-rev-en.svg');
    expect(branding.primaryLogoAlt.value).toBe('B.C. Government Logo');

    authenticate();

    expect(branding.secondaryLogoUrl.value).toBeNull();
    expect(branding.secondaryLogoAlt.value).toBeUndefined();
    expect(branding.backgroundImageUrl.value).toBeNull();
  });

  it('reacts to loaded default configuration and switching to BCSS and an unknown brand', () => {
    const configStore = useConfigStore();
    const branding = useBranding();
    configStore.config = { brandingOptions };

    expect(branding.applicationName.value).toBe('Default operations');
    expect(branding.dashboardTiles.value).toEqual(['myProfile', 'training']);

    configStore.config.selectedBrand = 'bcss';

    expect(branding.applicationName.value).toBe('BC Sheriff Service');
    expect(branding.primaryColor.value).toBe('#654321');
    expect(branding.backgroundColor.value).toBe('#dddddd');
    expect(branding.activeColor.value).toBe('#fedcba');
    expect(branding.brandingStyles.value).toEqual({
      '--ua-brand-blue': '#654321',
      '--ua-navigation-selected': '#fedcba',
    });
    expect(branding.dashboardTiles.value).toEqual(['schedule', 'myTeam']);
    expect(branding.dashboardMessage.value).toBe('BCSS message');

    authenticate();
    expect(branding.secondaryLogoUrl.value).toBe('/images/bcss-logo.svg');

    configStore.config.selectedBrand = 'unknown';

    expect(branding.applicationName.value).toBe('Default operations');
    expect(branding.primaryColor.value).toBe('#123456');
    expect(branding.backgroundColor.value).toBe('#eeeeee');
    expect(branding.activeColor.value).toBe('#abcdef');
    expect(branding.dashboardTiles.value).toEqual(['myProfile', 'training']);
    expect(branding.dashboardMessage.value).toBe('Default message');
    expect(branding.secondaryLogoUrl.value).toBeNull();
    expect(branding.secondaryLogoAlt.value).toBeUndefined();
    expect(branding.backgroundImageUrl.value).toBeNull();
  });

  it('shows BCSS logos and overlay only while authenticated and keeps the primary logo fixed', () => {
    useConfigStore().config = { selectedBrand: 'BCSS', brandingOptions };
    const branding = useBranding();

    expect(branding.secondaryLogoUrl.value).toBeNull();
    expect(branding.secondaryLogoAlt.value).toBeUndefined();
    expect(branding.backgroundImageUrl.value).toBeNull();
    expect(branding.dashboardBanner.value).toEqual(defaultBrand.dashboardBanner);
    expect(branding.primaryLogoUrl.value).toBe('/images/bcid-logo-rev-en.svg');

    authenticate();

    expect(branding.secondaryLogoUrl.value).toBe('/images/bcss-logo.svg');
    expect(branding.secondaryLogoAlt.value).toBe('BC Sheriff Service logo');
    expect(branding.backgroundImageUrl.value).toBe('/images/bcss-overlay.svg');
    expect(branding.dashboardBanner.value).toMatchObject({
      title: 'BCSS dashboard',
      subtitle: 'Sheriff operations',
    });
    expect(branding.primaryLogoUrl.value).toBe('/images/bcid-logo-rev-en.svg');
    expect(branding.primaryLogoAlt.value).toBe('B.C. Government Logo');

    useAuthStore().clearUserInfo();

    expect(branding.secondaryLogoUrl.value).toBeNull();
    expect(branding.secondaryLogoAlt.value).toBeUndefined();
    expect(branding.backgroundImageUrl.value).toBeNull();
    expect(branding.dashboardBanner.value).toEqual(defaultBrand.dashboardBanner);
  });
});
