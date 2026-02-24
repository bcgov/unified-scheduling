import { createFetch, type UseFetchOptions } from '@vueuse/core';
import { hasRefreshInProgress, isAuthTokenRequest, refreshAuthToken, waitForRefreshIfInProgress } from './authSession';

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE' | 'HEAD' | 'OPTIONS';

type QueryParamValue = string | number | boolean | null | undefined;
type QueryParams = Record<string, QueryParamValue>;

type RequestBody = unknown;

export type UseFetchAPIRequest = {
  url: string;
  method: HttpMethod;
  params?: QueryParams;
  headers?: HeadersInit;
  data?: RequestBody;
};

export type UseFetchAPIOptions = {
  fetchOptions?: RequestInit;
  options?: UseFetchOptions;
};

type AuthRetryRequestInit = RequestInit & {
  __authRetry?: boolean;
};

const getAuthStore = async () => {
  const { useAuthStore } = await import('@/stores/auth');
  return useAuthStore();
};

const buildQueryString = (params?: QueryParams) => {
  if (!params) {
    return '';
  }

  const searchParams = new URLSearchParams();

  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null) {
      return;
    }

    searchParams.append(key, String(value));
  });

  const queryString = searchParams.toString();
  return queryString ? `?${queryString}` : '';
};

const toBodyInit = (data: RequestBody, headers?: HeadersInit) => {
  if (data === undefined || data === null) {
    return undefined;
  }

  if (data instanceof FormData || data instanceof Blob || data instanceof URLSearchParams || typeof data === 'string') {
    return data;
  }

  if (data instanceof ArrayBuffer || ArrayBuffer.isView(data) || data instanceof ReadableStream) {
    return data;
  }

  const normalizedHeaders = new Headers(headers);
  const contentType = normalizedHeaders.get('Content-Type')?.toLowerCase();
  if (contentType?.includes('application/json')) {
    return JSON.stringify(data);
  }

  return JSON.stringify(data);
};

const fetchAPI = createFetch({
  baseUrl: '',
  combination: 'overwrite',
  options: {
    immediate: false,
    async beforeFetch({ url, options }) {
      const headers = new Headers(options.headers ?? undefined);
      if (!headers.has('Accept')) {
        headers.set('Accept', 'application/json');
      }

      if (!isAuthTokenRequest(url)) {
        await waitForRefreshIfInProgress();

        const authStore = await getAuthStore();

        if (authStore.isTokenExpired) {
          await refreshAuthToken();
        }

        if (authStore.token && !headers.has('Authorization')) {
          headers.set('Authorization', `Bearer ${authStore.token}`);
        }
      }

      return {
        options: {
          ...options,
          headers,
        },
      };
    },
    async onFetchError(ctx) {
      const response = ctx.response;
      const requestUrl = ctx.context.url;
      const requestOptions = ctx.context.options as AuthRetryRequestInit;

      if (!response || response.status !== 401 || isAuthTokenRequest(requestUrl)) {
        return ctx;
      }

      if (requestOptions.__authRetry) {
        return ctx;
      }

      const headers = new Headers(requestOptions.headers ?? undefined);

      if (hasRefreshInProgress()) {
        await waitForRefreshIfInProgress();
      } else {
        await refreshAuthToken();
      }

      const authStore = await getAuthStore();
      if (!authStore.token) {
        return ctx;
      }

      headers.set('Authorization', `Bearer ${authStore.token}`);

      ctx.context.options = {
        ...requestOptions,
        headers,
        __authRetry: true,
      } as AuthRetryRequestInit;

      await ctx.execute();

      return ctx;
    },
  },
});

export const useFetchAPI = <T>(
  { url, method, params, headers, data }: UseFetchAPIRequest,
  { fetchOptions, options }: UseFetchAPIOptions = {},
) => {
  const queryString = buildQueryString(params);
  const body = toBodyInit(data, headers);

  return fetchAPI<T>(
    `${url}${queryString}`,
    {
      ...fetchOptions,
      method,
      headers,
      body: body as BodyInit,
    },
    options,
  ).json<T>();
};
