import { useTranslation } from 'react-i18next';
import type { Dispatch, SetStateAction } from 'react';
import { sendBridgeCommand } from '../../../services/webViewBridge';
import { normalizePcGamingProfileName } from '../utils';
import { areTopologiesEqual, getProfileTopology, topologyToLegacy, validateTopology } from '../../../utils/displays';
import type { DisplayDeviceInfo, DisplayLayoutProfile, DisplayLayoutSnapshot, GamingProfileDisplayTopology } from '../../../types/bridge';
import type { SettingsPayloadOverrides } from '../../settings/buildSettingsPayload';
import type { PcGamingProfileBehaviourMap, PcGamingProfileControllerShortcutMap, PcGamingProfileHotkeysMap, PcGamingProfileLauncherMap } from '../utils';
import { canContinueWorkingProfile } from '../../setup/setupRouting';

export type DisplayProfileDraft = { profileName: string; topology: GamingProfileDisplayTopology };
export type DisplayValidationState = { showNameError: boolean; showSelectionError: boolean };
export type OverwriteProfileAction = 'next' | 'save' | 'save-then-continue';

type Options = {
  displayProfileDraftRef: { current: DisplayProfileDraft };
  displays: DisplayDeviceInfo[];
  profiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  isCreatingProfileFlow: boolean;
  isFirstRun: boolean;
  isEditingSection: boolean;
  pcGamingBehaviour: Record<string, boolean>;
  launcher: string;
  hotkeys: { pcGamingMode: string };
  profileBehaviours: PcGamingProfileBehaviourMap;
  profileLaunchers: PcGamingProfileLauncherMap;
  profileHotkeys: PcGamingProfileHotkeysMap;
  profileControllerShortcuts: PcGamingProfileControllerShortcutMap;
  setDisplays: Dispatch<SetStateAction<DisplayDeviceInfo[]>>;
  setIsRefreshingDisplays: Dispatch<SetStateAction<boolean>>;
  setDisplayListError: Dispatch<SetStateAction<string | null>>;
  setDisplayValidationState: Dispatch<SetStateAction<DisplayValidationState>>;
  setProfiles: Dispatch<SetStateAction<DisplayLayoutProfile[]>>;
  setActiveProfileName: Dispatch<SetStateAction<string | null>>;
  setLayout: Dispatch<SetStateAction<DisplayLayoutSnapshot | null>>;
  setSelectedProfileName: Dispatch<SetStateAction<string>>;
  setProfileBehaviours: Dispatch<SetStateAction<PcGamingProfileBehaviourMap>>;
  setProfileLaunchers: Dispatch<SetStateAction<PcGamingProfileLauncherMap>>;
  setProfileHotkeys: Dispatch<SetStateAction<PcGamingProfileHotkeysMap>>;
  setProfileControllerShortcuts: Dispatch<SetStateAction<PcGamingProfileControllerShortcutMap>>;
  pendingSettingsOverrideRef: { current: SettingsPayloadOverrides | null };
  setPendingOverwriteProfileAction: (value: SetStateAction<OverwriteProfileAction | null>) => void;
  setShowOverwriteProfileDialog: (value: boolean) => void;
  markUnsavedChanges: () => void;
  setError: (value: string | null) => void;
  setMessage: (value: string) => void;
  messages?: { invalid: string; noProfiles: string; unavailable: string; prepared: (name: string) => string; ready: (name: string) => string; empty: string };
};

export function useDisplayProfileWorkflow(options: Options) {
  const { t } = useTranslation();
  const messages = options.messages ?? {
    invalid: t('profile.validationMinDisplays'),
    noProfiles: t('profile.topologyEmpty'),
    unavailable: t('profile.displayLoadError'),
    prepared: (name: string) => t('profile.layoutPrepared', { name }),
    ready: (name: string) => name ? t('profile.layoutReadyContinue', { name }) : t('profile.refreshListUpdated'),
    empty: t('profile.noDisplaysFound')
  };
  const { displayProfileDraftRef, displays, profiles, selectedProfileName, isCreatingProfileFlow, isFirstRun, isEditingSection, pcGamingBehaviour, launcher, hotkeys, profileBehaviours, profileLaunchers, profileHotkeys, profileControllerShortcuts, setDisplays, setIsRefreshingDisplays, setDisplayListError, setDisplayValidationState, setProfiles, setActiveProfileName, setLayout, setSelectedProfileName, setProfileBehaviours, setProfileLaunchers, setProfileHotkeys, setProfileControllerShortcuts, pendingSettingsOverrideRef, setPendingOverwriteProfileAction, setShowOverwriteProfileDialog, markUnsavedChanges, setError, setMessage } = options;

  async function refreshDisplays() {
    setIsRefreshingDisplays(true); setDisplayListError(null); setError(null);
    try {
      const detectedDisplays = await sendBridgeCommand<DisplayDeviceInfo[]>('getDisplays');
      setDisplays(detectedDisplays); setMessage(detectedDisplays.length === 0 ? messages.empty : messages.ready(''));
    } catch (reason: unknown) { setDisplayListError(reason instanceof Error ? reason.message : messages.unavailable); }
    finally { setIsRefreshingDisplays(false); }
  }

  async function saveSelectedPcGamingLayout(profileName: string, topology: GamingProfileDisplayTopology) {
    const trimmedName = normalizePcGamingProfileName(profileName);
    const topologyErrors = validateTopology(topology);
    if (!trimmedName || topologyErrors.length > 0) { setError(topologyErrors[0] ?? messages.invalid); return; }
    const existingProfile = profiles.find((profile) => profile.name.toLowerCase() === trimmedName.toLowerCase()) ?? null;
    const selectedProfile = isEditingSection
      ? profiles.find((profile) => profile.name.toLowerCase() === selectedProfileName.toLowerCase()) ?? null
      : null;
    const isRenamingSelectedProfile = Boolean(selectedProfile && !existingProfile);
    const profileBeingUpdated = isRenamingSelectedProfile ? selectedProfile : existingProfile;
    const resolvedProfileName = profileBeingUpdated?.name === trimmedName ? profileBeingUpdated.name : trimmedName;
    const nextProfiles = profileBeingUpdated
      ? profiles.map((profile) => profile.name.toLowerCase() === profileBeingUpdated.name.toLowerCase() ? { ...profile, name: resolvedProfileName, displayTopology: topology, layout: topologyToLegacy(topology, displays) } : profile)
      : [...profiles, { name: trimmedName, displayTopology: topology, layout: topologyToLegacy(topology, displays) }];
    const sourceProfileName = profileBeingUpdated?.name;
    const nextProfileBehaviours = { ...profileBehaviours };
    const nextProfileLaunchers = { ...profileLaunchers };
    const nextProfileHotkeys = { ...profileHotkeys };
    const nextProfileControllerShortcuts = { ...profileControllerShortcuts };
    if (sourceProfileName && sourceProfileName !== resolvedProfileName) {
      nextProfileBehaviours[resolvedProfileName] = nextProfileBehaviours[sourceProfileName] ?? pcGamingBehaviour;
      nextProfileLaunchers[resolvedProfileName] = nextProfileLaunchers[sourceProfileName] ?? launcher;
      nextProfileHotkeys[resolvedProfileName] = nextProfileHotkeys[sourceProfileName] ?? { enterProfile: hotkeys.pcGamingMode };
      nextProfileControllerShortcuts[resolvedProfileName] = nextProfileControllerShortcuts[sourceProfileName] ?? null;
      delete nextProfileBehaviours[sourceProfileName];
      delete nextProfileLaunchers[sourceProfileName];
      delete nextProfileHotkeys[sourceProfileName];
      delete nextProfileControllerShortcuts[sourceProfileName];
    } else {
      nextProfileBehaviours[resolvedProfileName] = nextProfileBehaviours[resolvedProfileName] ?? pcGamingBehaviour;
      nextProfileLaunchers[resolvedProfileName] = nextProfileLaunchers[resolvedProfileName] ?? launcher;
      nextProfileHotkeys[resolvedProfileName] = nextProfileHotkeys[resolvedProfileName] ?? (profileBeingUpdated ? { enterProfile: hotkeys.pcGamingMode } : { enterProfile: '' });
      nextProfileControllerShortcuts[resolvedProfileName] = nextProfileControllerShortcuts[resolvedProfileName] ?? (profileBeingUpdated ? null : null);
    }
    const layout = topologyToLegacy(topology, displays);
    setProfiles(nextProfiles); setActiveProfileName(resolvedProfileName); setLayout(layout); setSelectedProfileName(resolvedProfileName); setProfileBehaviours(nextProfileBehaviours); setProfileLaunchers(nextProfileLaunchers); setProfileHotkeys(nextProfileHotkeys); setProfileControllerShortcuts(nextProfileControllerShortcuts);
    pendingSettingsOverrideRef.current = { pcGamingProfiles: nextProfiles, activePcGamingProfileName: resolvedProfileName, pcGamingLayout: layout, pcGamingProfileBehaviours: nextProfileBehaviours, pcGamingProfileLaunchers: nextProfileLaunchers, pcGamingProfileHotkeys: nextProfileHotkeys, pcGamingProfileControllerShortcuts: nextProfileControllerShortcuts };
    markUnsavedChanges(); setError(null); setMessage(isEditingSection ? messages.prepared(resolvedProfileName) : messages.ready(resolvedProfileName));
  }

  async function commitGamingDisplayDraft(allowOverwrite: boolean, overwriteAction: OverwriteProfileAction) {
    const draft = displayProfileDraftRef.current; const profileName = draft.profileName.trim(); const topology = draft.topology;
    const topologyErrors = validateTopology(topology);
    setDisplayValidationState({ showNameError: profileName.length === 0, showSelectionError: topology.displays.length === 0 });
    const hasDraft = profileName.length > 0 && topology.displays.length > 0;
    if ((profileName.length > 0 || topology.displays.length > 0) && !hasDraft || !canContinueWorkingProfile(isFirstRun ? 'FirstRun' : isCreatingProfileFlow ? 'CreateProfile' : isEditingSection ? 'EditProfile' : 'None', profileName, topology.displays.length)) { setError(messages.invalid); return false; }
    if (topologyErrors.length > 0) { setError(topologyErrors[0]); return false; }
    if (hasDraft) {
      const existingProfile = profiles.find((profile) => profile.name.toLowerCase() === profileName.toLowerCase()) ?? null;
      const isEditingSelectedProfile = selectedProfileName.length > 0 && selectedProfileName.toLowerCase() === profileName.toLowerCase();
      const isLayoutUnchanged = existingProfile ? areTopologiesEqual(topology, getProfileTopology(existingProfile)) : false;
      if (existingProfile && !isEditingSelectedProfile && !isLayoutUnchanged && !allowOverwrite) { setPendingOverwriteProfileAction(overwriteAction); setShowOverwriteProfileDialog(true); return false; }
      if (!existingProfile || !isLayoutUnchanged) await saveSelectedPcGamingLayout(profileName, topology);
    }
    setDisplayValidationState({ showNameError: false, showSelectionError: false }); return true;
  }

  return { refreshDisplays, saveSelectedPcGamingLayout, commitGamingDisplayDraft };
}
