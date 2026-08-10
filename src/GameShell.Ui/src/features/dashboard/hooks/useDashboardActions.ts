import { sendBridgeCommand } from '../../../services/webViewBridge';
import { formatCommandTargetMode } from '../../../utils/status';
import { getModeActionKey, type ModeCommand } from '../../../utils/modes';
import type { AppStatus, DisplayLayoutProfile, DisplayLayoutSnapshot, ModeResult } from '../../../types/bridge';
import type { PcGamingProfileBehaviourMap, PcGamingProfileControllerShortcutMap, PcGamingProfileHotkeysMap, PcGamingProfileLauncherMap } from '../../profiles/utils';
import type { SettingsPayloadOverrides } from '../../settings/buildSettingsPayload';

type Options = {
  hasUnsavedChanges: boolean;
  isSetupComplete: boolean;
  selectedProfileName: string;
  profiles: DisplayLayoutProfile[];
  activeProfileName: string | null;
  profileBehaviours: PcGamingProfileBehaviourMap;
  profileLaunchers: PcGamingProfileLauncherMap;
  profileHotkeys: PcGamingProfileHotkeysMap;
  profileControllerShortcuts: PcGamingProfileControllerShortcutMap;
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
  const { hasUnsavedChanges, isSetupComplete, selectedProfileName, profiles, activeProfileName, profileBehaviours, profileLaunchers, profileHotkeys, profileControllerShortcuts, saveCurrentSettings, setStatus, setActiveModeAction, setIsRemovingProfile, setActiveProfileName, setSelectedProfileName, setLayout, setError, setMessage } = options;

  async function runModeCommand(command: ModeCommand, saveBeforeRun = false, payload?: unknown) {
    setActiveModeAction(getModeActionKey(command)); setError(null); setMessage(`Przełączanie na ${formatCommandTargetMode(command)}...`);
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
          await saveCurrentSettings(isSetupComplete, { activePcGamingProfileName: matched.name, pcGamingLayout: matched.layout });
        }
      }
      setMessage(command === 'enterWorkMode' && result.message === 'Enter Work Mode completed.' ? 'Przełączono do Work Mode.' : result.message);
    } catch (reason: unknown) { setError(reason instanceof Error ? reason.message : 'Nie udało się uruchomić trybu.'); }
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
    setError(null); setIsRemovingProfile(true); setMessage(`Usuwanie profilu ${profileName}...`);
    try {
      await saveCurrentSettings(true, { pcGamingProfiles: nextProfiles, activePcGamingProfileName: nextActiveProfileName, pcGamingLayout: nextLayout, pcGamingProfileBehaviours: nextProfileBehaviours, pcGamingProfileLaunchers: nextProfileLaunchers, pcGamingProfileHotkeys: nextProfileHotkeys, pcGamingProfileControllerShortcuts: nextProfileControllerShortcuts });
      setMessage(`Usunięto profil ${profileName}.`);
    } catch (reason: unknown) { setError(reason instanceof Error ? reason.message : 'Nie udało się usunąć profilu.'); }
    finally { setIsRemovingProfile(false); }
  }

  return { runModeCommand, deleteSelectedProfile };
}
