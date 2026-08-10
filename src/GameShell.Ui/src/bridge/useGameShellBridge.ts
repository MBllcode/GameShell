import { useEffect, useRef } from 'react';
import { sendBridgeCommand, subscribeBridgeEvent } from '../services/webViewBridge';
import type { AppSettings, AppStatus, ControllerStatusInfo, DisplayDeviceInfo } from '../types/bridge';

export function useGameShellBridge({
  onStatus,
  onSettings,
  onDisplays,
  onDisplayError,
  onControllerStatus,
  onError,
  onInitialLoadingChange
}: {
  onStatus: (status: AppStatus) => void;
  onSettings: (settings: AppSettings) => void;
  onDisplays: (displays: DisplayDeviceInfo[]) => void;
  onDisplayError: (message: string) => void;
  onControllerStatus: (status: ControllerStatusInfo) => void;
  onError: (message: string) => void;
  onInitialLoadingChange: (isLoading: boolean) => void;
}) {
  const callbacksRef = useRef({ onStatus, onSettings, onDisplays, onDisplayError, onControllerStatus, onError, onInitialLoadingChange });
  callbacksRef.current = { onStatus, onSettings, onDisplays, onDisplayError, onControllerStatus, onError, onInitialLoadingChange };

  useEffect(() => {
    let isMounted = true;
    Promise.all([
      sendBridgeCommand<AppStatus>('getStatus'),
      sendBridgeCommand<AppSettings>('getSettings'),
      sendBridgeCommand<DisplayDeviceInfo[]>('getDisplays').catch((reason: unknown) => {
        console.error('[GameShell] getDisplays failed', reason);
        if (isMounted) {
          callbacksRef.current.onDisplayError(reason instanceof Error ? reason.message : 'Nie udało się odczytać listy ekranów.');
        }
        return [];
      })
    ])
      .then(([status, settings, displays]) => {
        if (!isMounted) return;
        console.info(`[GameShell] getDisplays returned ${displays.length} display(s)`);
        callbacksRef.current.onStatus(status);
        callbacksRef.current.onSettings(settings);
        callbacksRef.current.onDisplays(displays);
        callbacksRef.current.onInitialLoadingChange(false);
      })
      .catch((reason: unknown) => {
        if (isMounted) {
          callbacksRef.current.onError(reason instanceof Error ? reason.message : 'Nie udało się odczytać statusu aplikacji.');
          callbacksRef.current.onInitialLoadingChange(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  useEffect(() => subscribeBridgeEvent('statusChanged', async () => {
    try {
      callbacksRef.current.onStatus(await sendBridgeCommand<AppStatus>('getStatus'));
    } catch (reason: unknown) {
      callbacksRef.current.onError(reason instanceof Error ? reason.message : 'Nie udało się odświeżyć statusu aplikacji.');
    }
  }), []);

  useEffect(() => {
    let isDisposed = false;
    const unsubscribe = subscribeBridgeEvent('controllerStatusChanged', async (eventData) => {
      const event = eventData as { sequenceNumber?: number };
      console.info(`[GameShell] UI received controllerStatusChanged #${event.sequenceNumber ?? 'unknown'}`);
      try {
        const nextStatus = await sendBridgeCommand<ControllerStatusInfo>('getControllerStatus');
        if (!isDisposed) callbacksRef.current.onControllerStatus(nextStatus);
      } catch (reason: unknown) {
        callbacksRef.current.onError(reason instanceof Error ? reason.message : 'Nie udało się odświeżyć statusu kontrolera.');
      }
    });

    void sendBridgeCommand<ControllerStatusInfo>('getControllerStatus')
      .then((nextStatus) => {
        if (!isDisposed) callbacksRef.current.onControllerStatus(nextStatus);
      })
      .catch((reason: unknown) => {
        if (!isDisposed) callbacksRef.current.onError(reason instanceof Error ? reason.message : 'Nie udało się odczytać statusu kontrolera.');
      });

    return () => {
      isDisposed = true;
      unsubscribe();
    };
  }, []);
}
