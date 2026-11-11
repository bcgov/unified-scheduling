import { computed } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { getBrandingConfig } from '@/branding';
import { defaultBrand, fixedPrimaryLogo } from '@/branding/brandingConfig';

/**
 * Auth-gated view of the branding configuration.
 *
 * Custom branding (logo + background overlay image) is only applied once the
 * user is authenticated. Unauthenticated users (e.g. on the pre-login screen)
 * always see the default B.C. Government logo and no background overlay.
 */
export const useBranding = () => {
  const authStore = useAuthStore();
  const brand = computed(() => getBrandingConfig());

  // Custom branding always applies once the user has logged in.
  const isBrandingActive = computed(() => authStore.isAuthenticated);

  const primaryLogo = computed(() => (isBrandingActive.value ? brand.value.primaryLogo : fixedPrimaryLogo));
  const primaryLogoUrl = computed(() => primaryLogo.value.url);
  const primaryLogoAlt = computed(() => primaryLogo.value.alt ?? '');
  const secondaryLogo = computed(() => (isBrandingActive.value ? brand.value.secondaryLogo : null));
  const secondaryLogoUrl = computed(() => secondaryLogo.value?.url || null);
  const secondaryLogoAlt = computed(() => (secondaryLogo.value?.url ? (secondaryLogo.value.alt ?? '') : undefined));
  const backgroundImageUrl = computed(() => (isBrandingActive.value ? brand.value.backgroundImageUrl || null : null));
  const applicationName = computed(() => brand.value.applicationName);
  const primaryColor = computed(() => brand.value.primaryColor);
  const backgroundColor = computed(() => brand.value.backgroundColor);
  const activeColor = computed(() => brand.value.activeColor);
  const brandingStyles = computed(() => ({
    '--ua-brand-blue': primaryColor.value,
    '--ua-navigation-selected': activeColor.value,
  }));
  const dashboardTiles = computed(() => brand.value.dashboardTiles);
  const dashboardMessage = computed(() => brand.value.dashboardMessage);
  const dashboardBanner = computed(() =>
    isBrandingActive.value ? brand.value.dashboardBanner : defaultBrand.dashboardBanner,
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
