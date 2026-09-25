export const setupMockServiceWorker = async (): Promise<void> => {
  if (!import.meta.env.DEV) {
    return;
  }

  const { handlers } = await import('./handlers');
  // Skip registering the service worker entirely when there are no active
  // handlers. Otherwise MSW intercepts every request on every page (adding
  // latency) and its internal passthrough-to-network call can throw an
  // uncaught "Failed to fetch" when a request is aborted mid-flight (e.g. HMR
  // reloads), with no functional benefit since nothing is actually mocked.
  if (handlers.length === 0) {
    return;
  }

  const { worker } = await import('./browser');
  try {
    await worker.start({
      serviceWorker: {
        url: `${import.meta.env.BASE_URL}mockServiceWorker.js`,
      },
      onUnhandledRequest: 'bypass',
    });
  } catch (error) {
    console.warn('[MSW] Service worker registration failed. Continuing without mocks.', error);
  }
};
