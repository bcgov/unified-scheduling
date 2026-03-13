export const setupMockServiceWorker = async (): Promise<void> => {
  if (!import.meta.env.DEV) {
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
