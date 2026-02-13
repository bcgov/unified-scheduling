import { ref, computed } from 'vue';
import { defineStore } from 'pinia';

export interface AuthToken {
  access_token: string;
  expires_at: string;
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(null);
  const tokenExpiry = ref<string | null>(null);

  const isAuthenticated = computed(() => !!token.value);

  const isTokenExpired = computed(() => {
    if (!tokenExpiry.value) return true;
    return new Date() > new Date(tokenExpiry.value);
  });

  const setToken = (authToken: AuthToken) => {
    token.value = authToken.access_token;
    tokenExpiry.value = authToken.expires_at;
  };

  const clearToken = () => {
    token.value = null;
    tokenExpiry.value = null;
  };

  const refreshToken = async (): Promise<void> => {
    try {
      // @TODO: get base url
      const baseUrl = '';
      const response = await fetch(`${baseUrl}/api/Auth/token`, {
        credentials: 'include',
        headers: {
          Accept: 'application/json',
        },
      });

      if (response.status === 200) {
        const data: AuthToken = await response.json();

        if (data.access_token == null) {
          // Redirect to login if no token received
          const redirectUri = encodeURIComponent(window.location.href);
          window.location.href = `${baseUrl}/api/auth/login?redirectUri=${redirectUri}`;
          return;
        }

        setToken(data);
      } else {
        clearToken();
        throw new Error('Failed to refresh token');
      }
    } catch (error) {
      console.error('Error refreshing token:', error);
      clearToken();
      throw error;
    }
  };

  const login = (redirectUri?: string) => {
    const baseUrl = '';
    const redirect = redirectUri || window.location.href;
    window.location.href = `${baseUrl}/api/auth/login?redirectUri=${encodeURIComponent(redirect)}`;
  };

  const logout = async () => {
    try {
      const baseUrl = '';
      await fetch(`${baseUrl}/api/auth/logout`, {
        method: 'POST',
        credentials: 'include',
      });
    } finally {
      clearToken();
    }
  };

  return {
    token,
    tokenExpiry,
    isAuthenticated,
    isTokenExpired,
    setToken,
    clearToken,
    refreshToken,
    login,
    logout,
  };
});
