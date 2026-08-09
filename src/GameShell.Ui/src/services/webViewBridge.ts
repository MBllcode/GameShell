import type { BridgeCommand, BridgeRequest, BridgeResponse } from '../types/bridge';

type PendingRequest = {
  resolve: (value: unknown) => void;
  reject: (reason?: unknown) => void;
};

const pendingRequests = new Map<string, PendingRequest>();
const eventListeners = new Map<string, Set<(eventData: unknown) => void>>();
let listenerAttached = false;

export function sendBridgeCommand<T>(type: BridgeCommand, payload?: unknown): Promise<T> {
  const webview = window.chrome?.webview;

  if (!webview) {
    return Promise.reject(new Error('Połączenie z aplikacją jest niedostępne.'));
  }

  attachResponseListener(webview);

  const requestId = createRequestId();
  const request: BridgeRequest = { type, requestId, payload };

  return new Promise<T>((resolve, reject) => {
    pendingRequests.set(requestId, {
      resolve: (value) => resolve(value as T),
      reject
    });

    webview.postMessage(request);
  });
}

export function subscribeBridgeEvent(type: string, listener: (eventData: unknown) => void | Promise<void>) {
  const webview = window.chrome?.webview;
  if (webview) {
    attachResponseListener(webview);
  }

  const listeners = eventListeners.get(type) ?? new Set<(eventData: unknown) => void>();
  const wrapped = (eventData: unknown) => {
    void listener(eventData);
  };
  listeners.add(wrapped);
  eventListeners.set(type, listeners);

  return () => {
    listeners.delete(wrapped);
    if (listeners.size === 0) {
      eventListeners.delete(type);
    }
  };
}

function attachResponseListener(webview: NonNullable<Window['chrome']>['webview']) {
  if (listenerAttached || !webview) {
    return;
  }

  webview.addEventListener('message', (event: MessageEvent) => {
    if (typeof event.data !== 'object' || event.data === null) {
      return;
    }

    const response = event.data as BridgeResponse<unknown>;
    const bridgeEvent = event.data as { type?: string };
    if (bridgeEvent.type && !('requestId' in bridgeEvent)) {
      eventListeners.get(bridgeEvent.type)?.forEach((listener) => listener(event.data));
      return;
    }

    const pending = pendingRequests.get(response.requestId);

    if (!pending) {
      return;
    }

    pendingRequests.delete(response.requestId);

    if (response.success) {
      pending.resolve(response.data);
      return;
    }

    pending.reject(new Error(response.error || 'Bridge command failed.'));
  });

  listenerAttached = true;
}

function createRequestId() {
  if (globalThis.crypto?.randomUUID) {
    return globalThis.crypto.randomUUID();
  }

  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}
