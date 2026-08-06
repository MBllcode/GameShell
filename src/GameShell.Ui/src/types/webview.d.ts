import type { BridgeRequest } from './bridge';

declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (message: BridgeRequest) => void;
        addEventListener: (type: 'message', listener: (event: MessageEvent) => void) => void;
      };
    };
  }
}

export {};
