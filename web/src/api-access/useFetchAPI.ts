import { createFetch, type UseFetchOptions } from '@vueuse/core';

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

      return {
        options: {
          ...options,
          headers,
        },
      };
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
