import { ControllerFamily, mapControllerButtons } from '../controllerDisplayMapper.ts';
import type { ControllerShortcutSettings, ControllerStatusInfo } from '../types/bridge.ts';

export function normalizeControllerButtons(buttons: string[] | null | undefined) {
  return [...new Set(
    (Array.isArray(buttons) ? buttons : [])
      .filter((button): button is string => typeof button === 'string')
      .map((button) => button.trim())
      .filter(Boolean)
  )];
}

export function normalizeControllerStatus(status: ControllerStatusInfo): ControllerStatusInfo {
  return {
    ...status,
    sequenceNumber: status.sequenceNumber ?? 0,
    capturedButtons: normalizeControllerButtons(status.capturedButtons),
    lastReadButtons: normalizeControllerButtons(status.lastReadButtons),
    completedCapture: status.completedCapture
      ? {
        ...status.completedCapture,
        buttons: normalizeControllerButtons(status.completedCapture.buttons)
      }
      : null
  };
}

export function isControllerCapturePhase(phase: ControllerStatusInfo['capturePhase']) {
  return phase === 'WaitingForRelease'
    || phase === 'WaitingForFirstPress'
    || phase === 'Recording'
    || phase === 'WaitingForCombinationRelease';
}

export function normalizeControllerShortcut(shortcut: ControllerShortcutSettings | null | undefined): ControllerShortcutSettings | null {
  if (!shortcut) {
    return null;
  }

  const buttons = normalizeControllerButtons(shortcut.buttons ?? []);
  if (buttons.length === 0) {
    return null;
  }

  return {
    buttons,
    holdDurationMs: shortcut.holdDurationMs || 700
  };
}

export function isDuplicateProfileControllerShortcut(
  profileName: string,
  candidateButtons: string[],
  profileControllerShortcuts: Record<string, ControllerShortcutSettings | null>
) {
  if (candidateButtons.length === 0) {
    return false;
  }

  const normalizedCandidate = getControllerShortcutKey(candidateButtons);
  const currentOwnerId = normalizeOwnerId(profileName);

  return Object.entries(profileControllerShortcuts).some(([name, shortcut]) => {
    if (normalizeOwnerId(name) === currentOwnerId) {
      return false;
    }

    return getControllerShortcutKey(shortcut?.buttons) === normalizedCandidate;
  });
}

export function isDuplicateWorkModeControllerShortcut(
  candidateButtons: string[],
  profileControllerShortcuts: Record<string, ControllerShortcutSettings | null>
) {
  const normalizedCandidate = getControllerShortcutKey(candidateButtons);
  return normalizedCandidate.length > 0 && Object.values(profileControllerShortcuts).some((shortcut) => (
    getControllerShortcutKey(shortcut?.buttons) === normalizedCandidate
  ));
}

export function isSameControllerShortcut(left: string[] | undefined, right: string[] | undefined) {
  const leftKey = getControllerShortcutKey(left);
  return leftKey.length > 0 && leftKey === getControllerShortcutKey(right);
}

function getControllerShortcutKey(buttons: string[] | undefined) {
  return normalizeControllerButtons(buttons).sort((left, right) => left.localeCompare(right)).join('+').toUpperCase();
}

function normalizeOwnerId(value: string) {
  return value.trim().toUpperCase();
}

export function formatControllerShortcutButtons(buttons: string[], family: ControllerFamily) {
  return mapControllerButtons(normalizeControllerButtons(buttons), family)
    .join(' + ');
}
