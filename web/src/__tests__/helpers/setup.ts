import { afterAll, afterEach, beforeAll } from 'vitest';

import { server } from '../mocks/server';

// Polyfill visualViewport for Vuetify overlay components in happy-dom environment
if (typeof window !== 'undefined' && !window.visualViewport) {
  type VisualViewportListener = (this: VisualViewport, ev: Event) => unknown;
  const eventListeners: Record<string, Set<VisualViewportListener>> = {};

  Object.defineProperty(window, 'visualViewport', {
    value: {
      width: 1024,
      height: 768,
      offsetLeft: 0,
      offsetTop: 0,
      pageLeft: 0,
      pageTop: 0,
      scale: 1,
      addEventListener: (event: string, listener: VisualViewportListener) => {
        if (!eventListeners[event]) {
          eventListeners[event] = new Set();
        }
        eventListeners[event].add(listener);
      },
      removeEventListener: (event: string, listener: VisualViewportListener) => {
        if (eventListeners[event]) {
          eventListeners[event].delete(listener);
        }
      },
    },
    writable: true,
    configurable: true,
  });
}

beforeAll(() => {
  server.listen({ onUnhandledRequest: 'bypass' });
});

afterEach(() => {
  server.resetHandlers();
});

afterAll(() => {
  server.close();
});
