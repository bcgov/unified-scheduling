import { createFetch, type UseFetchOptions } from '@vueuse/core';
import { computed, unref, type MaybeRef } from 'vue';

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE' | 'HEAD' | 'OPTIONS';

type QueryParamValue = string | number | boolean | null | undefined;
type QueryParams = Record<string, QueryParamValue>;

type RequestBody = unknown;
type FetchErrorContext = Parameters<NonNullable<UseFetchOptions['onFetchError']>>[0];
type AfterFetchContext = Parameters<NonNullable<UseFetchOptions['afterFetch']>>[0];

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

const parseResponseText = (data: unknown) => {
  if (typeof data !== 'string') {
    return data;
  }

  const text = data.trim();
  if (!text) {
    return null;
  }

  try {
    return JSON.parse(text) as unknown;
  } catch {
    return data;
  }
};

const getPayloadMessage = (payload: unknown) => {
  if (typeof payload === 'string') {
    return payload.trim();
  }

  if (!payload || typeof payload !== 'object') {
    return '';
  }

  const candidate = payload as { message?: unknown; detail?: unknown; title?: unknown };
  return [candidate.message, candidate.detail, candidate.title].find(
    (value): value is string => typeof value === 'string' && value.trim().length > 0,
  );
};

const buildFetchError = (payload: unknown, fallback: unknown) => {
  const message = getPayloadMessage(payload);
  if (message) {
    return new Error(message);
  }

  if (fallback instanceof Error) {
    return fallback;
  }

  return new Error(typeof fallback === 'string' && fallback ? fallback : 'Request failed.');
};

const buildUseFetchOptions = (options?: UseFetchOptions): UseFetchOptions => {
  const userAfterFetch = options?.afterFetch;
  const userOnFetchError = options?.onFetchError;

  return {
    ...options,
    updateDataOnError: options?.updateDataOnError ?? true,
    async afterFetch(ctx: AfterFetchContext) {
      const nextContext = {
        ...ctx,
        data: parseResponseText(ctx.data),
      };

      return userAfterFetch ? userAfterFetch(nextContext) : nextContext;
    },
    async onFetchError(ctx: FetchErrorContext) {
      const parsedData = parseResponseText(ctx.data);
      const nextContext = {
        ...ctx,
        data: parsedData,
        error: buildFetchError(parsedData, ctx.error),
      };

      return userOnFetchError ? userOnFetchError(nextContext) : nextContext;
    },
  };
};

const fetchAPI = createFetch({
  baseUrl: '',
  combination: 'overwrite',
  options: {
    immediate: true,
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

  const request = fetchAPI<T>(
    reactiveUrl,
    {
      ...fetchOptions,
      method,
      headers,
      body: body as BodyInit,
    },
    buildUseFetchOptions(options),
  );

  return request.text() as ReturnType<typeof request.json<T>>;
};
