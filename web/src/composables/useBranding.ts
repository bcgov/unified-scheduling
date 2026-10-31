import { computed } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { getBrandingConfig } from '@/branding';
import { defaultBrand, fixedPrimaryLogo } from '@/branding/brandingConfig';
import { resolveBrandAssetUrl } from '@/branding/brandAssetUrl';

/**
 * Auth-gated view of the branding configuration.
 *
 * Custom branding (logo + background overlay image) is only applied once the
 * user is authenticated. Unauthenticated users (e.g. on the pre-login screen)
 * always see the default B.C. Government logo and no background overlay.
 */
export const useBranding = () => {
  const authStore = useAuthStore();

  // Custom branding always applies once the user has logged in.
  const isBrandingActive = computed(() => authStore.isAuthenticated);

  const primaryLogoUrl = computed(() => resolveBrandAssetUrl(fixedPrimaryLogo.url));
  const primaryLogoAlt = computed(() => fixedPrimaryLogo.alt);
  const secondaryLogoUrl = computed(() =>
    isBrandingActive.value && getBrandingConfig().secondaryLogoUrl ? getBrandingConfig().secondaryLogoUrl : null,
  );
  const secondaryLogoAlt = computed(() =>
    isBrandingActive.value && getBrandingConfig().secondaryLogoUrl ? getBrandingConfig().secondaryLogoAlt : undefined,
  );
  const backgroundImageUrl = computed(() =>
    isBrandingActive.value && getBrandingConfig().backgroundImageUrl ? getBrandingConfig().backgroundImageUrl : null,
  );
  const applicationName = computed(() => getBrandingConfig().applicationName);
  const primaryColor = computed(() => getBrandingConfig().primaryColor);
  const backgroundColor = computed(() => getBrandingConfig().backgroundColor);
  const activeColor = computed(() => getBrandingConfig().activeColor);
  const brandingStyles = computed(() => ({
    '--ua-brand-blue': primaryColor.value,
    '--ua-navigation-selected': activeColor.value,
  }));
  const dashboardTiles = computed(() => getBrandingConfig().dashboardTiles);
  const dashboardMessage = computed(() => getBrandingConfig().dashboardMessage);
  const dashboardBanner = computed(() =>
    isBrandingActive.value ? getBrandingConfig().dashboardBanner : defaultBrand.dashboardBanner,
  );

  return {
    brandingStyles,
    primaryLogoUrl,
    primaryLogoAlt,
    secondaryLogoUrl,
    secondaryLogoAlt,
    backgroundImageUrl,
    applicationName,
    primaryColor,
    backgroundColor,
    activeColor,
    dashboardTiles,
    dashboardMessage,
    dashboardBanner,
  };
};
