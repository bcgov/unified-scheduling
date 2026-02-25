import { useFetch } from '@vueuse/core';
import type { TokenResponse } from './generated/models';

type RequestAuthTokenResult = {
  statusCode?: number | null;
  data?: TokenResponse | null;
};

const AUTH_TOKEN_PATH = '/api/auth/token';
const AUTH_LOGIN_PATH = '/api/auth/login';

let refreshTokenPromise: Promise<void> | null = null;

const getAuthStore = async () => {
  const { useAuthStore } = await import('@/stores/auth');
  return useAuthStore();
};

const redirectToLogin = () => {
  const redirectUri = encodeURIComponent(window.location.href);
  window.location.href = `${AUTH_LOGIN_PATH}?redirectUri=${redirectUri}`;
};

export const isAuthTokenRequest = (url: string) => {
  try {
    return new URL(url, window.location.origin).pathname.endsWith(AUTH_TOKEN_PATH);
  } catch {
    return url.endsWith(AUTH_TOKEN_PATH);
  }
};

export const hasRefreshInProgress = () => !!refreshTokenPromise;

export const waitForRefreshIfInProgress = async () => {
  if (refreshTokenPromise) {
    await refreshTokenPromise;
  }
};

export const requestAuthToken = async (): Promise<RequestAuthTokenResult> => {
  try {
    const tokenRequest = useFetch(
      AUTH_TOKEN_PATH,
      {
        method: 'GET',
        headers: {
          Accept: 'application/json',
        },
        credentials: 'include',
      },
      {
        immediate: false,
      },
    ).json<TokenResponse>();

    await tokenRequest.execute(true);

    return {
      statusCode: tokenRequest.statusCode.value,
      data: tokenRequest.data.value,
    };
  } catch {
    return {
      statusCode: null,
      data: null,
    };
  }
};

export const refreshAuthToken = async () => {
  if (!refreshTokenPromise) {
    refreshTokenPromise = (async () => {
      const authStore = await getAuthStore();
      const { statusCode, data } = await requestAuthToken();

      if (statusCode !== 200) {
        authStore.clearToken();
        throw new Error('Failed to refresh token');
      }

      if (!data?.accessToken) {
        authStore.clearToken();
        redirectToLogin();
        return;
      }

      authStore.setToken(data);
    })().finally(() => {
      refreshTokenPromise = null;
    });
  }

  return refreshTokenPromise;
};

export const initializeAuthSession = async (): Promise<boolean> => {
  try {
    await refreshAuthToken();
  } catch (error) {
    console.error(error);
    // @TODO: Something is wrong on the server side so redirect to unathenticated page and show a login button
    // Redirecting to login page again here create infinite loop if the service is down or there is some other issue,
    return false;
  }

  const authStore = await getAuthStore();
  return !!authStore.token;
};
