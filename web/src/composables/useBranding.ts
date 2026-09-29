import { computed } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { getBrandingConfig } from '@/branding';
import { defaultBrand } from '@/branding/default';

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

  const primaryLogoUrl = computed(() =>
    isBrandingActive.value && getBrandingConfig().primaryLogoEnabled
      ? getBrandingConfig().primaryLogoUrl
      : defaultBrand.primaryLogoUrl,
  );
  const primaryLogoAlt = computed(() =>
    isBrandingActive.value && getBrandingConfig().primaryLogoEnabled
      ? getBrandingConfig().primaryLogoAlt
      : defaultBrand.primaryLogoAlt,
  );
  const secondaryLogoUrl = computed(() =>
    isBrandingActive.value && getBrandingConfig().secondaryLogoEnabled ? getBrandingConfig().secondaryLogoUrl : null,
  );
  const secondaryLogoAlt = computed(() =>
    isBrandingActive.value && getBrandingConfig().secondaryLogoEnabled
      ? getBrandingConfig().secondaryLogoAlt
      : undefined,
  );
  const backgroundImageUrl = computed(() =>
    isBrandingActive.value && getBrandingConfig().backgroundImageEnabled
      ? getBrandingConfig().backgroundImageUrl
      : null,
  );
  const applicationName = computed(() => getBrandingConfig().applicationName);

  return {
    primaryLogoUrl,
    primaryLogoAlt,
    secondaryLogoUrl,
    secondaryLogoAlt,
    backgroundImageUrl,
    applicationName,
  };
};
