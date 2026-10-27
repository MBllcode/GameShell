import type { BridgeRequest } from './contracts';

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
