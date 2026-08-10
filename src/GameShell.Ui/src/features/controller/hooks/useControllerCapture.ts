import { useEffect, useRef, useState } from 'react';
import { ControllerFamily, detectControllerFamily } from '../../../controllerDisplayMapper';
import { sendBridgeCommand } from '../../../services/webViewBridge';
import type { ControllerShortcutSettings, ControllerStatusInfo } from '../../../types/bridge';
import { isControllerCapturePhase, normalizeControllerButtons, normalizeControllerStatus } from '../../../utils/controllers';

const workModeCaptureId = 'WorkMode';
const defaultHoldDurationMs = 700;

const initialControllerStatus: ControllerStatusInfo = {
  sequenceNumber: 0,
  isConnected: false,
  deviceName: null,
  isListening: false,
  capturePhase: 'Idle',
  captureSessionId: null,
  profileId: null,
  startedAt: null,
  capturedButtons: [],
  completedCapture: null,
  inputSource: 'None',
  gamepadButtonCount: 0,
  joystickButtonCount: 0,
  rawPressedButtons: [],
  backend: 'SDL3',
  controllerCount: 0,
  controllerId: null,
  lastReadButtons: [],
  lastReadAt: null,
  diagnosticStatus: 'Brak zgodnego kontrolera'
};

type ShortcutMap = Record<string, ControllerShortcutSettings | null>;

export function useControllerCapture({
  selectedProfileName,
  profileShortcuts,
  workModeShortcut,
  onProfileShortcutCaptured,
  onWorkModeShortcutCaptured,
  onError
}: {
  selectedProfileName: string;
  profileShortcuts: ShortcutMap;
  workModeShortcut: ControllerShortcutSettings | null;
  onProfileShortcutCaptured: (profileName: string, buttons: string[]) => void;
  onWorkModeShortcutCaptured: (buttons: string[]) => void;
  onError: (message: string) => void;
}) {
  const [status, setStatus] = useState(initialControllerStatus);
  const [captureSuccessButtons, setCaptureSuccessButtons] = useState<string[] | null>(null);
  const [notice, setNotice] = useState<'missing' | 'disconnected' | null>(null);
  const sequenceRef = useRef(0);
  const sessionRef = useRef<string | null>(null);
  const captureActiveRef = useRef(false);
  const acknowledgedRef = useRef<string | null>(null);
  const completionTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const previousProfileRef = useRef(selectedProfileName);

  function applyStatus(nextStatus: ControllerStatusInfo) {
    const normalizedStatus = normalizeControllerStatus(nextStatus);
    if (normalizedStatus.sequenceNumber < sequenceRef.current) {
      console.info(`[GameShell] Ignored stale controller status #${normalizedStatus.sequenceNumber}; current #${sequenceRef.current}`);
      return status;
    }

    sequenceRef.current = normalizedStatus.sequenceNumber;
    console.info(`[GameShell] UI controller status updated #${normalizedStatus.sequenceNumber} Connected=${normalizedStatus.isConnected} Family=${detectControllerFamily(normalizedStatus.deviceName)}`);
    const wasCapturing = captureActiveRef.current;
    captureActiveRef.current = isControllerCapturePhase(normalizedStatus.capturePhase);
    if (normalizedStatus.controllerCount > 0) setNotice(null);
    else if (wasCapturing) setNotice('disconnected');
    sessionRef.current = normalizedStatus.captureSessionId;
    setStatus(normalizedStatus);
    return normalizedStatus;
  }

  async function startCapture(profileId: string) {
    setCaptureSuccessButtons(null);
    try {
      const currentStatus = applyStatus(await sendBridgeCommand<ControllerStatusInfo>('getControllerStatus'));
      if (currentStatus.controllerCount === 0) {
        setNotice('missing');
        return;
      }
      setNotice(null);
      await sendBridgeCommand('startControllerShortcutCapture', { profileId });
      applyStatus(await sendBridgeCommand<ControllerStatusInfo>('getControllerStatus'));
    } catch {
      onError('Nie udało się rozpocząć nasłuchiwania kontrolera.');
    }
  }

  async function cancelCapture() {
    if (completionTimerRef.current) {
      clearTimeout(completionTimerRef.current);
      completionTimerRef.current = null;
    }
    setCaptureSuccessButtons(null);
    try {
      const nextStatus = await sendBridgeCommand('cancelControllerShortcutCapture', { reason: 'UserCancelled' })
        .then(() => sendBridgeCommand<ControllerStatusInfo>('getControllerStatus'));
      applyStatus(nextStatus);
    } catch {
      onError('Nie udało się anulować nasłuchiwania kontrolera.');
    }
  }

  useEffect(() => {
    const phase = status.capturePhase;
    if (!(isControllerCapturePhase(phase) || phase === 'Completed')) return;
    let disposed = false;
    const refresh = async () => {
      try {
        const nextStatus = await sendBridgeCommand<ControllerStatusInfo>('getControllerStatus');
        if (!disposed) {
          const normalizedStatus = applyStatus(nextStatus);
          console.info(`[GameShell] UI capture phase: ${normalizedStatus.capturePhase}`);
        }
      } catch {
        // Push events remain the primary path; polling is a short-lived recovery path.
      }
    };
    const timer = setInterval(() => void refresh(), 100);
    return () => {
      disposed = true;
      clearInterval(timer);
    };
  }, [status.capturePhase]);

  useEffect(() => {
    const completedCapture = status.completedCapture;
    if (!completedCapture || completedCapture.sessionId !== status.captureSessionId) return;
    const nextButtons = normalizeControllerButtons(completedCapture.buttons);
    if (nextButtons.length === 0) return;
    const currentButtons = completedCapture.profileId === workModeCaptureId
      ? workModeShortcut?.buttons ?? []
      : profileShortcuts[completedCapture.profileId]?.buttons ?? [];
    const unchanged = currentButtons.length === nextButtons.length && currentButtons.every((button, index) => button === nextButtons[index]);
    if (!unchanged) {
      if (completedCapture.profileId === workModeCaptureId) onWorkModeShortcutCaptured(nextButtons);
      else onProfileShortcutCaptured(completedCapture.profileId, nextButtons);
    }
    if (acknowledgedRef.current === completedCapture.sessionId) return;
    acknowledgedRef.current = completedCapture.sessionId;
    setCaptureSuccessButtons(nextButtons);
    void sendBridgeCommand<ControllerStatusInfo>('acknowledgeControllerShortcutCapture', { sessionId: completedCapture.sessionId })
      .then(applyStatus)
      .catch(() => {
        acknowledgedRef.current = null;
        onError('Nie udało się potwierdzić zapisania kombinacji kontrolera.');
      });
    completionTimerRef.current = setTimeout(() => setCaptureSuccessButtons(null), 1500);
  }, [onError, onProfileShortcutCaptured, onWorkModeShortcutCaptured, profileShortcuts, status.completedCapture, status.captureSessionId, workModeShortcut]);

  useEffect(() => () => {
    if (completionTimerRef.current) clearTimeout(completionTimerRef.current);
  }, []);

  useEffect(() => {
    const previousProfile = previousProfileRef.current;
    previousProfileRef.current = selectedProfileName;
    if (previousProfile && previousProfile !== selectedProfileName && sessionRef.current) {
      void sendBridgeCommand('cancelControllerShortcutCapture', { reason: 'ProfileChanged' }).catch(() => undefined);
    }
  }, [selectedProfileName]);

  return {
    status,
    applyStatus,
    captureSuccessButtons,
    notice,
    startCapture,
    cancelCapture,
    cancelActiveCapture: () => sendBridgeCommand('cancelControllerShortcutCapture', { reason: 'SectionLeft' }).catch(() => undefined),
    sessionRef
  };
}

export { defaultHoldDurationMs, workModeCaptureId };
export type { ShortcutMap };
export type ControllerCaptureApi = ReturnType<typeof useControllerCapture>;
