import { createFetch, type UseFetchOptions } from '@vueuse/core';
import { computed, unref, type MaybeRef } from 'vue';

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE' | 'HEAD' | 'OPTIONS';

type QueryParamValue = string | number | boolean | null | undefined;
type QueryParams = Record<string, QueryParamValue>;

type RequestBody = unknown;

export type UseFetchAPIRequest = {
  url: string;
  method: HttpMethod;
  params?: MaybeRef<QueryParams>;
  headers?: HeadersInit;
  data?: RequestBody;
};

export type UseFetchAPIOptions = {
  fetchOptions?: RequestInit;
  options?: UseFetchOptions;
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
    immediate: true,
    updateDataOnError: true, // adds error response data to data property and error property stays as Error instance
    async onFetchError(ctx) {
      // Handle Global api call errors here
      // Return ctx to allow the error to propagate to the caller
      return ctx;
    },
  },
});

export const useFetchAPI = <T>(
  { url, method, params, headers, data }: UseFetchAPIRequest,
  { fetchOptions, options }: UseFetchAPIOptions = {},
) => {
  const body = toBodyInit(data, headers);

  // Build reactive URL with query params
  const reactiveUrl = computed(() => {
    const queryString = buildQueryString(unref(params));
    return `${url}${queryString}`;
  });

  return fetchAPI<T>(
    reactiveUrl,
    {
      ...fetchOptions,
      method,
      headers,
      body: body as BodyInit,
    },
    options,
  ).json<T>();
};
