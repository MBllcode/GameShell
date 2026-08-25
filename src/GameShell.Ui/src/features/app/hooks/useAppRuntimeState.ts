import { useState } from 'react';
import type { AppStatus } from '../../../types/bridge';

export const initialStatus: AppStatus = {
  state: 'Unknown',
  currentMode: null,
  sessionState: 'Idle',
  isBusy: false,
  displayMode: 'NativeDisplayConfig',
  selectedConsoleDisplay: null,
  hasGamingProfiles: false,
  activeProfileName: null,
  activeLauncher: null,
  lastOperation: null,
  lastOperationDurationMs: null,
  lastError: null,
  transientOperation: {
    kind: 'None',
    phase: 'None',
    isActive: false,
    messageKey: null,
    startedAt: null,
    transitionId: null
  },
  settingsPath: ''
};

export function useAppRuntimeState() {
  const [status, setStatus] = useState<AppStatus>(initialStatus);
  const [settings, setSettings] = useState<import('../../../types/bridge').AppSettings | null>(null);
  const [isRemovingProfile, setIsRemovingProfile] = useState(false);
  return {
    status,
    setStatus,
    settings,
    setSettings,
    isRemovingProfile,
    setIsRemovingProfile
  };
}
