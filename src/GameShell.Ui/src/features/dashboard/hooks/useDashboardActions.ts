import { useTranslation } from 'react-i18next';
import { sendBridgeCommand } from '../../../services/webViewBridge';
import { formatCommandTargetMode } from '../../../utils/status';
import { getModeActionKey, type ModeCommand } from '../../../utils/modes';
import type { AppStatus, DisplayLayoutProfile, DisplayLayoutSnapshot, ModeResult } from '../../../types/bridge';
import type { GamingProfileBehaviourMap, GamingProfileControllerShortcutMap, GamingProfileHotkeysMap, GamingProfileLauncherMap } from '../../profiles/utils';
import type { SettingsPayloadOverrides } from '../../settings/buildSettingsPayload';

type Options = {
  hasUnsavedChanges: boolean;
  isSetupComplete: boolean;
  selectedProfileName: string;
  profiles: DisplayLayoutProfile[];
  activeProfileName: string | null;
  profileBehaviours: GamingProfileBehaviourMap;
  profileLaunchers: GamingProfileLauncherMap;
  profileHotkeys: GamingProfileHotkeysMap;
  profileControllerShortcuts: GamingProfileControllerShortcutMap;
  saveCurrentSettings: (isFirstRunCompleted?: boolean, overrides?: SettingsPayloadOverrides) => Promise<unknown>;
  setStatus: (value: AppStatus | ((current: AppStatus) => AppStatus)) => void;
  setActiveModeAction: (value: string | null) => void;
  setIsRemovingProfile: (value: boolean) => void;
  setActiveProfileName: (value: string | null) => void;
  setSelectedProfileName: (value: string) => void;
  setLayout: (value: DisplayLayoutSnapshot | null) => void;
  setError: (value: string | null) => void;
  setMessage: (value: string) => void;
};

export function useDashboardActions(options: Options) {
  const { t } = useTranslation();
  const { hasUnsavedChanges, isSetupComplete, selectedProfileName, profiles, activeProfileName, profileBehaviours, profileLaunchers, profileHotkeys, profileControllerShortcuts, saveCurrentSettings, setStatus, setActiveModeAction, setIsRemovingProfile, setActiveProfileName, setSelectedProfileName, setLayout, setError, setMessage } = options;

  async function runModeCommand(command: ModeCommand, saveBeforeRun = false, payload?: unknown) {
    setActiveModeAction(getModeActionKey(command)); setError(null); setMessage(t('dashboard.switchingTo', { mode: formatCommandTargetMode(command, t) }));
    try {
      if (saveBeforeRun || hasUnsavedChanges) await saveCurrentSettings(isSetupComplete);
      const result = await sendBridgeCommand<ModeResult>(command, payload);
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
          await saveCurrentSettings(isSetupComplete, { activeGamingProfileName: matched.name, gamingLayout: matched.layout });
        }
      }
      setMessage(command === 'enterWorkMode' && result.message === 'Enter Work Mode completed.' ? t('dashboard.switchComplete') : result.message);
    } catch (reason: unknown) { setError(reason instanceof Error ? reason.message : t('dashboard.runModeError')); }
    finally { setActiveModeAction(null); }
  }

  async function deleteSelectedProfile() {
    const profileName = selectedProfileName.trim(); if (!profileName) return;
    const profileKey = profileName.toLowerCase(); const nextProfiles = profiles.filter((profile) => profile.name.toLowerCase() !== profileKey);
    if (nextProfiles.length === profiles.length) return;
    const filterMap = <T,>(map: Record<string, T>) => Object.fromEntries(Object.entries(map).filter(([name]) => name.toLowerCase() !== profileKey)) as Record<string, T>;
    const nextProfileBehaviours = filterMap(profileBehaviours); const nextProfileLaunchers = filterMap(profileLaunchers); const nextProfileHotkeys = filterMap(profileHotkeys); const nextProfileControllerShortcuts = filterMap(profileControllerShortcuts);
    const deletedActiveProfile = activeProfileName?.toLowerCase() === profileKey;
    const nextActiveProfileName = deletedActiveProfile ? nextProfiles[0]?.name ?? null : activeProfileName && nextProfiles.some((profile) => profile.name === activeProfileName) ? activeProfileName : nextProfiles[0]?.name ?? null;
    const nextLayout = nextActiveProfileName ? nextProfiles.find((profile) => profile.name === nextActiveProfileName)?.layout ?? null : null;
    setError(null); setIsRemovingProfile(true); setMessage(t('dashboard.deleteInProgress', { name: profileName }));
    try {
      await saveCurrentSettings(true, { gamingProfiles: nextProfiles, activeGamingProfileName: nextActiveProfileName, gamingLayout: nextLayout, gamingProfileBehaviours: nextProfileBehaviours, gamingProfileLaunchers: nextProfileLaunchers, gamingProfileHotkeys: nextProfileHotkeys, gamingProfileControllerShortcuts: nextProfileControllerShortcuts });
      setMessage(t('dashboard.deleteSuccess', { name: profileName }));
    } catch (reason: unknown) { setError(reason instanceof Error ? reason.message : t('dashboard.deleteError')); }
    finally { setIsRemovingProfile(false); }
  }

  return { runModeCommand, deleteSelectedProfile };
}
