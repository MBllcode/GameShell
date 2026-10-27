import type { Profile } from '../../profiles/profileModel';
import { useTranslation } from 'react-i18next';
import { sendBridgeCommand } from '../../../bridge/webViewBridge';
import { type ModeCommand } from '../../profiles/profileCommands';
import type { AppStatus, DisplayLayoutSnapshot, ModeResult } from '../../../bridge/contracts';
import type { SettingsPayloadOverrides } from '../../settings/buildSettingsPayload';

type Options = {
  hasUnsavedChanges: boolean;
  isSetupComplete: boolean;
  selectedProfileName: string;
  profiles: Profile[];
  activeProfileName: string | null;
  saveCurrentSettings: (isFirstRunCompleted?: boolean, overrides?: SettingsPayloadOverrides) => Promise<unknown>;
  setStatus: (value: AppStatus | ((current: AppStatus) => AppStatus)) => void;
  setIsRemovingProfile: (value: boolean) => void;
  setActiveProfileName: (value: string | null) => void;
  setSelectedProfileName: (value: string) => void;
  setLayout: (value: DisplayLayoutSnapshot | null) => void;
  setError: (value: string | null) => void;
  setMessage: (value: string) => void;
  setTransientMessage: (value: string, durationMs?: number) => void;
};

export function useDashboardActions(options: Options) {
  const { t } = useTranslation();
  const { hasUnsavedChanges, isSetupComplete, selectedProfileName, profiles, activeProfileName, saveCurrentSettings, setStatus, setIsRemovingProfile, setActiveProfileName, setSelectedProfileName, setLayout, setError, setMessage, setTransientMessage } = options;

  async function runModeCommand(command: ModeCommand, saveBeforeRun = false, payload?: unknown) {
    setError(null);
    try {
      if (saveBeforeRun || hasUnsavedChanges) await saveCurrentSettings(isSetupComplete);
      const modeResult = await sendBridgeCommand<ModeResult>(command, payload);
      if (!modeResult.success) {
        setError(modeResult.message || t('dashboard.runModeError'));
        return;
      }
      if (command === 'enterWorkMode') {
        console.info('[GETSTATUS REQUEST FROM UI]', { timestamp: new Date().toISOString(), source: 'enterWorkMode completion' });
      }
      const nextStatus = await sendBridgeCommand<AppStatus>('getStatus');
      if (command === 'enterWorkMode') {
        console.info('[UI STATUS RECEIVED]', {
          timestamp: new Date().toISOString(),
          currentMode: nextStatus.currentMode,
          sessionState: nextStatus.sessionState,
          activeProfileName: nextStatus.activeProfileName,
          activeLauncher: nextStatus.activeLauncher,
          source: 'enterWorkMode completion'
        });
      }
      setStatus(nextStatus);
      if (command === 'enterWorkMode' && nextStatus.currentMode === 'Work' && nextStatus.sessionState === 'Idle') {
        setActiveProfileName(null);
      }
      if (command === 'enterGameShellWithProfile' && typeof payload === 'object' && payload !== null && 'profileName' in payload) {
        const requestedName = String((payload as { profileName?: unknown }).profileName ?? '').trim();
        const matched = profiles.find((profile) => profile.name.toLowerCase() === requestedName.toLowerCase());
        if (matched) {
          setActiveProfileName(matched.name); setSelectedProfileName(matched.name); setLayout(matched.layout);
          if (modeResult?.status !== 'NoChanges') {
            await saveCurrentSettings(isSetupComplete, { activeGamingProfileName: matched.name, gamingLayout: matched.layout });
          }
        }
      }
      if (modeResult?.status === 'NoChanges') {
        setTransientMessage(t('dashboard.profileAlreadyActive'));
      } else if (command === 'enterWorkMode') {
        setTransientMessage(t('dashboard.enterWorkModeCompleted'));
      } else {
        setTransientMessage(t('dashboard.profileActivated', { name: nextStatus.activeProfileName || selectedProfileName }));
      }
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : t('dashboard.runModeError'));
    }
  }

  async function deleteSelectedProfile() {
    const profileName = selectedProfileName.trim(); if (!profileName) return;
    const profileKey = profileName.toLowerCase(); const nextProfiles = profiles.filter((profile) => profile.name.toLowerCase() !== profileKey);
    if (nextProfiles.length === profiles.length) return;
    const deletedActiveProfile = activeProfileName?.toLowerCase() === profileKey;
    const nextActiveProfileName = deletedActiveProfile ? nextProfiles[0]?.name ?? null : activeProfileName && nextProfiles.some((profile) => profile.name === activeProfileName) ? activeProfileName : nextProfiles[0]?.name ?? null;
    const nextLayout = nextActiveProfileName ? nextProfiles.find((profile) => profile.name === nextActiveProfileName)?.layout ?? null : null;
    setError(null); setIsRemovingProfile(true); setMessage(t('dashboard.deleteInProgress', { name: profileName }));
    try {
      await saveCurrentSettings(true, { gamingProfiles: nextProfiles, activeGamingProfileName: nextActiveProfileName, gamingLayout: nextLayout });
      setTransientMessage(t('dashboard.deleteSuccess', { name: profileName }));
    } catch (reason: unknown) { setError(reason instanceof Error ? reason.message : t('dashboard.deleteError')); }
    finally { setIsRemovingProfile(false); }
  }

  return { runModeCommand, deleteSelectedProfile };
}
