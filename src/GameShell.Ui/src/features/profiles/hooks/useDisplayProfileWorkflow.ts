import { createProfile, type Profile } from '../profileModel';
import type { LauncherType } from '../profileCommands';
import { useTranslation } from 'react-i18next';
import { useRef, type Dispatch, type SetStateAction } from 'react';
import { sendBridgeCommand } from '../../../bridge/webViewBridge';
import { normalizeGamingProfileName } from '../utils';
import { areTopologiesEqual, getProfileTopology, topologyToLegacy, validateTopology } from '../../displays/displayTopology';
import type { DisplayDeviceInfo, DisplayLayoutSnapshot, GamingProfileDisplayTopology } from '../../../bridge/contracts';
import type { SettingsPayloadOverrides } from '../../settings/buildSettingsPayload';
import { canContinueWorkingProfile } from '../../setup/setupRouting';

export type DisplayProfileDraft = { profileName: string; topology: GamingProfileDisplayTopology };
export type DisplayValidationState = { showNameError: boolean; showSelectionError: boolean };
export type OverwriteProfileAction = 'next' | 'save' | 'save-then-continue';

function createProfileId(): string {
  if (typeof globalThis.crypto?.randomUUID === 'function') {
    return globalThis.crypto.randomUUID();
  }

  return `profile-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

type Options = {
  displayProfileDraftRef: { current: DisplayProfileDraft };
  displays: DisplayDeviceInfo[];
  profiles: Profile[];
  selectedProfileName: string;
  isCreatingProfileFlow: boolean;
  isFirstRun: boolean;
  isEditingSection: boolean;
  gamingBehaviour: Record<string, boolean>;
  launcher: LauncherType;
  setDisplays: Dispatch<SetStateAction<DisplayDeviceInfo[]>>;
  setIsRefreshingDisplays: Dispatch<SetStateAction<boolean>>;
  setDisplayListError: Dispatch<SetStateAction<string | null>>;
  setDisplayValidationState: Dispatch<SetStateAction<DisplayValidationState>>;
  setProfiles: Dispatch<SetStateAction<Profile[]>>;
  setActiveProfileName: Dispatch<SetStateAction<string | null>>;
  setLayout: Dispatch<SetStateAction<DisplayLayoutSnapshot | null>>;
  setSelectedProfileName: Dispatch<SetStateAction<string>>;
  pendingSettingsOverrideRef: { current: SettingsPayloadOverrides | null };
  setPendingOverwriteProfileAction: (value: SetStateAction<OverwriteProfileAction | null>) => void;
  setShowOverwriteProfileDialog: (value: boolean) => void;
  markUnsavedChanges: () => void;
  setError: (value: string | null) => void;
  setMessage: (value: string) => void;
  messages?: { invalid: string; noProfiles: string; unavailable: string; prepared: (name: string) => string; ready: (name: string) => string; empty: string };
};

export function useDisplayProfileWorkflow(options: Options) {
  // The optional first-run profile is provisional until the wizard is finished.
  const firstRunProfileId = useRef<string | null>(null);
  const { t } = useTranslation();
  const messages = options.messages ?? {
    invalid: t('profile.validationMinDisplays'),
    noProfiles: t('profile.topologyEmpty'),
    unavailable: t('profile.displayLoadError'),
    prepared: (name: string) => t('profile.layoutPrepared', { name }),
    ready: (name: string) => name ? t('profile.layoutReadyContinue', { name }) : t('profile.refreshListUpdated'),
    empty: t('profile.noDisplaysFound')
  };
  const { displayProfileDraftRef, displays, profiles, selectedProfileName, isCreatingProfileFlow, isFirstRun, isEditingSection, gamingBehaviour, launcher, setDisplays, setIsRefreshingDisplays, setDisplayListError, setDisplayValidationState, setProfiles, setActiveProfileName, setLayout, setSelectedProfileName, pendingSettingsOverrideRef, setPendingOverwriteProfileAction, setShowOverwriteProfileDialog, markUnsavedChanges, setError, setMessage } = options;

  async function refreshDisplays() {
    setIsRefreshingDisplays(true); setDisplayListError(null); setError(null);
    try {
      const detectedDisplays = await sendBridgeCommand<DisplayDeviceInfo[]>('getDisplays');
      setDisplays(detectedDisplays); setMessage(detectedDisplays.length === 0 ? messages.empty : messages.ready(''));
    } catch (reason: unknown) { setDisplayListError(reason instanceof Error ? reason.message : messages.unavailable); }
    finally { setIsRefreshingDisplays(false); }
  }

  async function saveSelectedGamingLayout(profileName: string, topology: GamingProfileDisplayTopology) {
    const trimmedName = normalizeGamingProfileName(profileName);
    const topologyErrors = validateTopology(topology);
    if (!trimmedName || topologyErrors.length > 0) { setError(topologyErrors[0] ?? messages.invalid); return; }
    const existingProfile = profiles.find((profile) => profile.name.toLowerCase() === trimmedName.toLowerCase()) ?? null;
    const selectedProfile = isEditingSection
      ? profiles.find((profile) => profile.name.toLowerCase() === selectedProfileName.toLowerCase()) ?? null
      : null;
    const isRenamingSelectedProfile = Boolean(selectedProfile && !existingProfile);
    const profileBeingUpdated = isRenamingSelectedProfile ? selectedProfile : existingProfile;
    const resolvedProfileName = profileBeingUpdated?.name === trimmedName ? profileBeingUpdated.name : trimmedName;
    const layout = topologyToLegacy(topology, displays);
    const updated = profileBeingUpdated
      ? { ...profileBeingUpdated, name: resolvedProfileName, displayTopology: topology, layout }
      : createProfile({ id: createProfileId(), name: trimmedName, displayTopology: topology, layout }, {
          behaviour: { ...gamingBehaviour } as Profile['behaviour'], application: launcher
        });
    const nextProfiles = profileBeingUpdated
      ? profiles.map(profile => profile === profileBeingUpdated ? updated : profile)
      : [...profiles, updated];
    if (isFirstRun && !profileBeingUpdated) firstRunProfileId.current = updated.id ?? null;
    setProfiles(nextProfiles);
    setActiveProfileName(resolvedProfileName);
    setLayout(layout);
    setSelectedProfileName(resolvedProfileName);
    pendingSettingsOverrideRef.current = { gamingProfiles: nextProfiles, activeGamingProfileName: resolvedProfileName, gamingLayout: layout };
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
      if (!existingProfile || !isLayoutUnchanged) await saveSelectedGamingLayout(profileName, topology);
    }
    setDisplayValidationState({ showNameError: false, showSelectionError: false }); return true;
  }

  function discardFirstRunProfile() {
    if (!isFirstRun) return;
    const id = firstRunProfileId.current;
    if (id) setProfiles(current => current.filter(profile => profile.id !== id));
    firstRunProfileId.current = null;
    setActiveProfileName(null);
    setSelectedProfileName('');
    setLayout(null);
    pendingSettingsOverrideRef.current = null;
    setDisplayValidationState({ showNameError: false, showSelectionError: false });
  }

  return { refreshDisplays, saveSelectedGamingLayout, commitGamingDisplayDraft, discardFirstRunProfile };
}
