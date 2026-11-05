import type {
  LoanOutEntryRequest,
  LoanOutEntryResponse,
  LoanOutCalendarUser,
  LoanOutUserSearchRequest,
  LoanOutSeriesRequest,
  LoanOutSeriesResponse,
} from '@/api-access/generated/models';
import { useFetchAPI, type UseFetchAPIOptions } from '@/api-access/useFetchAPI';

export type {
  LoanOutCalendarUser,
  LoanOutEntryRequest,
  LoanOutEntryResponse,
  LoanOutSeriesRequest,
  LoanOutSeriesResponse,
  LoanOutUserSearchRequest,
};

const loanOutEntryUrl = (id: number) => `/api/scheduling/loan-out/entries/${id}`;
const loanOutSeriesUrl = (id: number) => `/api/scheduling/loan-out/series/${id}`;

function readOptions(signal?: AbortSignal): UseFetchAPIOptions {
  return {
    options: {
      immediate: false,
      beforeFetch({ options }) {
        // VueUse replaces fetchOptions.signal on execute; merge after its controller is created.
        if (signal) options.signal = options.signal ? AbortSignal.any([options.signal, signal]) : signal;
        return { options };
      },
    },
  };
}

export function useUserLoanOutEntries(userId: string, signal?: AbortSignal) {
  return useFetchAPI<LoanOutEntryResponse[]>(
    { url: `/api/scheduling/loan-out/users/${userId}/entries`, method: 'GET' },
    readOptions(signal),
  );
}

export function useLoanOutSchedulingUsers(request: LoanOutUserSearchRequest, signal?: AbortSignal) {
  return useFetchAPI<LoanOutCalendarUser[]>(
    {
      url: '/api/loan-out/scheduling/users',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: request,
    },
    readOptions(signal),
  );
}

export function useUserLoanOutSeries(userId: string, signal?: AbortSignal) {
  return useFetchAPI<LoanOutSeriesResponse[]>(
    { url: `/api/scheduling/loan-out/users/${userId}/series`, method: 'GET' },
    readOptions(signal),
  );
}

export async function getLoanOutEntry(id: number, signal?: AbortSignal) {
  const result = useFetchAPI<LoanOutEntryResponse>({ url: loanOutEntryUrl(id), method: 'GET' }, readOptions(signal));
  await result.execute();
  return result;
}

export async function getLoanOutSeries(id: number, signal?: AbortSignal) {
  const result = useFetchAPI<LoanOutSeriesResponse>({ url: loanOutSeriesUrl(id), method: 'GET' }, readOptions(signal));
  await result.execute();
  return result;
}

export async function createLoanOutEntry(body: LoanOutEntryRequest) {
  const result = useFetchAPI<LoanOutEntryResponse>(
    {
      url: '/api/scheduling/loan-out/entries',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: body,
    },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

export async function createLoanOutSeries(body: LoanOutSeriesRequest) {
  const result = useFetchAPI<LoanOutSeriesResponse>(
    {
      url: '/api/scheduling/loan-out/series',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: body,
    },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

export async function updateLoanOutEntry(id: number, body: LoanOutEntryRequest) {
  const result = useFetchAPI<LoanOutEntryResponse>(
    { url: loanOutEntryUrl(id), method: 'PUT', headers: { 'Content-Type': 'application/json' }, data: body },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

export async function updateLoanOutSeries(id: number, body: LoanOutSeriesRequest) {
  const result = useFetchAPI<LoanOutSeriesResponse>(
    { url: loanOutSeriesUrl(id), method: 'PUT', headers: { 'Content-Type': 'application/json' }, data: body },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

export async function deleteLoanOutEntry(id: number) {
  const result = useFetchAPI<void>({ url: loanOutEntryUrl(id), method: 'DELETE' }, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function deleteLoanOutSeries(id: number) {
  const result = useFetchAPI<void>({ url: loanOutSeriesUrl(id), method: 'DELETE' }, { options: { immediate: false } });
  await result.execute();
  return result;
}
