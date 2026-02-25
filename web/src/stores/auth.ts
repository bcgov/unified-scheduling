import { ref, computed } from 'vue';
import { defineStore } from 'pinia';
import type { TokenResponse } from '@/api-access/generated/models';

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(null);
  const tokenExpiry = ref<string | null>(null);

  const isAuthenticated = computed(() => !!token.value);

  const isTokenExpired = computed(() => {
    if (!tokenExpiry.value) return true;
    return new Date() > new Date(tokenExpiry.value);
  });

  const setToken = (authToken: TokenResponse) => {
    if (authToken.accessToken) token.value = authToken.accessToken;
    if (authToken.expiresAt) tokenExpiry.value = authToken.expiresAt;
  };

  const clearToken = () => {
    token.value = null;
    tokenExpiry.value = null;
  };

  return {
    token,
    tokenExpiry,
    isAuthenticated,
    isTokenExpired,
    setToken,
    clearToken,
  };
});
