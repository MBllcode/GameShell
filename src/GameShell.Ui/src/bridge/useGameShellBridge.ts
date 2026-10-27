import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { sendBridgeCommand, subscribeBridgeEvent } from './webViewBridge';
import type { AppSettings, AppStatus, ControllerStatusInfo, DisplayDeviceInfo, DisplayDiscoveryState } from './contracts';

export function useGameShellBridge({
  onStatus,
  onSettings,
  onDisplays,
  onDisplayError,
  onDisplayDiscoveryState,
  onControllerStatus,
  onError,
  onInitialLoadingChange
  ,onHydrationComplete
}: {
  onStatus: (status: AppStatus) => void;
  onSettings: (settings: AppSettings) => void;
  onDisplays: (displays: DisplayDeviceInfo[]) => void;
  onDisplayError: (message: string) => void;
  onDisplayDiscoveryState: (state: DisplayDiscoveryState) => void;
  onControllerStatus: (status: ControllerStatusInfo) => void;
  onError: (message: string) => void;
  onInitialLoadingChange: (isLoading: boolean) => void;
  onHydrationComplete: () => void;
}) {
  const { t } = useTranslation();
  const callbacksRef = useRef({ onStatus, onSettings, onDisplays, onDisplayError, onDisplayDiscoveryState, onControllerStatus, onError, onInitialLoadingChange, onHydrationComplete });
  callbacksRef.current = { onStatus, onSettings, onDisplays, onDisplayError, onDisplayDiscoveryState, onControllerStatus, onError, onInitialLoadingChange, onHydrationComplete };

  useEffect(() => {
    let isMounted = true;
    Promise.all([
      sendBridgeCommand<AppStatus>('getStatus'),
      sendBridgeCommand<AppSettings>('getSettings'),
      sendBridgeCommand<DisplayDeviceInfo[]>('getDisplays').catch((reason: unknown) => {
        console.error('[GameShell] getDisplays failed', reason);
        if (isMounted) {
          callbacksRef.current.onDisplayError(reason instanceof Error ? reason.message : t('bridge.displayReadError'));
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
        callbacksRef.current.onHydrationComplete();
        callbacksRef.current.onInitialLoadingChange(false);
      })
      .catch((reason: unknown) => {
        if (isMounted) {
          callbacksRef.current.onError(reason instanceof Error ? reason.message : t('bridge.statusReadError'));
          callbacksRef.current.onHydrationComplete();
          callbacksRef.current.onInitialLoadingChange(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  useEffect(() => subscribeBridgeEvent('statusChanged', async () => {
    console.info('[STATUSCHANGED RECEIVED]', { timestamp: new Date().toISOString() });
    try {
      console.info('[GETSTATUS REQUEST FROM UI]', { timestamp: new Date().toISOString(), source: 'statusChanged' });
      const nextStatus = await sendBridgeCommand<AppStatus>('getStatus');
      console.info('[UI STATUS RECEIVED]', {
        timestamp: new Date().toISOString(),
        currentMode: nextStatus.currentMode,
        sessionState: nextStatus.sessionState,
        activeProfileName: nextStatus.activeProfileName,
        activeLauncher: nextStatus.activeLauncher
      });
      callbacksRef.current.onStatus(nextStatus);
    } catch (reason: unknown) {
      callbacksRef.current.onError(reason instanceof Error ? reason.message : t('bridge.statusRefreshError'));
    }
  }), []);

  useEffect(() => subscribeBridgeEvent('displayDiscoveryChanged', async (eventData) => {
    const event = eventData as { state?: DisplayDiscoveryState };
    const state = event.state;
    if (!state) return;
    callbacksRef.current.onDisplayDiscoveryState(state);
    if (state !== 'Ready') return;
    try {
      callbacksRef.current.onDisplays(await sendBridgeCommand<DisplayDeviceInfo[]>('getDisplays'));
    } catch (reason: unknown) {
      callbacksRef.current.onDisplayError(reason instanceof Error ? reason.message : t('bridge.displayReadError'));
    }
  }), [t]);

  useEffect(() => {
    let isDisposed = false;
    const unsubscribe = subscribeBridgeEvent('controllerStatusChanged', async (eventData) => {
      const event = eventData as { sequenceNumber?: number };
      console.info(`[GameShell] UI received controllerStatusChanged #${event.sequenceNumber ?? 'unknown'}`);
      try {
        const nextStatus = await sendBridgeCommand<ControllerStatusInfo>('getControllerStatus');
        if (!isDisposed) callbacksRef.current.onControllerStatus(nextStatus);
      } catch (reason: unknown) {
        callbacksRef.current.onError(reason instanceof Error ? reason.message : t('bridge.controllerRefreshError'));
      }
    });

    void sendBridgeCommand<ControllerStatusInfo>('getControllerStatus')
      .then((nextStatus) => {
        if (!isDisposed) callbacksRef.current.onControllerStatus(nextStatus);
      })
      .catch((reason: unknown) => {
        if (!isDisposed) callbacksRef.current.onError(reason instanceof Error ? reason.message : t('bridge.controllerReadError'));
      });

    return () => {
      isDisposed = true;
      unsubscribe();
    };
  }, []);
}
