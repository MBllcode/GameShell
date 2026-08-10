import type { Dispatch, SetStateAction } from 'react';
import { sendBridgeCommand } from '../../../services/webViewBridge';
import { areIdentitySetsEqual, extractDisplayIdentities, MAX_PROFILE_DISPLAYS, PROFILE_DISPLAY_LIMIT_MESSAGE, sameIdentity } from '../../../utils/displays';
import type { DisplayDeviceInfo, DisplayIdentity, DisplayLayoutProfile, DisplayLayoutSnapshot } from '../../../types/bridge';
import type { SettingsPayloadOverrides } from '../../settings/buildSettingsPayload';
import type { PcGamingProfileBehaviourMap, PcGamingProfileControllerShortcutMap, PcGamingProfileHotkeysMap, PcGamingProfileLauncherMap } from '../utils';

export type DisplayProfileDraft = { profileName: string; identities: DisplayIdentity[] };
export type DisplayValidationState = { showNameError: boolean; showSelectionError: boolean };
export type OverwriteProfileAction = 'next' | 'save' | 'save-then-continue';

type Options = {
  displayProfileDraftRef: { current: DisplayProfileDraft };
  displays: DisplayDeviceInfo[];
  profiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  isCreatingProfileFlow: boolean;
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
  const messages = options.messages ?? {
    invalid: 'Uzupełnij nazwę nowego profilu i wybierz co najmniej jeden ekran.',
    noProfiles: 'Dodaj profil i wybierz ekran, aby kontynuować.',
    unavailable: 'Nie udało się odczytać listy ekranów.',
    prepared: (name: string) => `Profil ekranów przygotowany: ${name}. Zapisz zmiany, aby zatwierdzić.`,
    ready: (name: string) => name ? `Profil ekranów gotowy: ${name}. Kliknij Dalej, aby kontynuować.` : 'Odświeżono listę ekranów.',
    empty: 'Nie znaleziono ekranów.'
  };
  const { displayProfileDraftRef, displays, profiles, selectedProfileName, isCreatingProfileFlow, isEditingSection, pcGamingBehaviour, launcher, hotkeys, profileBehaviours, profileLaunchers, profileHotkeys, profileControllerShortcuts, setDisplays, setIsRefreshingDisplays, setDisplayListError, setDisplayValidationState, setProfiles, setActiveProfileName, setLayout, setSelectedProfileName, setProfileBehaviours, setProfileLaunchers, setProfileHotkeys, setProfileControllerShortcuts, pendingSettingsOverrideRef, setPendingOverwriteProfileAction, setShowOverwriteProfileDialog, markUnsavedChanges, setError, setMessage } = options;

  async function refreshDisplays() {
    setIsRefreshingDisplays(true); setDisplayListError(null); setError(null);
    try {
      const detectedDisplays = await sendBridgeCommand<DisplayDeviceInfo[]>('getDisplays');
      setDisplays(detectedDisplays); setMessage(detectedDisplays.length === 0 ? messages.empty : messages.ready(''));
    } catch (reason: unknown) { setDisplayListError(reason instanceof Error ? reason.message : messages.unavailable); }
    finally { setIsRefreshingDisplays(false); }
  }

  async function saveSelectedPcGamingLayout(profileName: string, identities: DisplayIdentity[]) {
    const trimmedName = profileName.trim();
    if (!trimmedName || identities.length === 0) { setError(messages.invalid); return; }
    if (identities.length > MAX_PROFILE_DISPLAYS) { setError(PROFILE_DISPLAY_LIMIT_MESSAGE); return; }
    const selectedDevices = identities.map((identity) => displays.find((display) => sameIdentity(display.identity, identity)) ?? null).filter((display): display is DisplayDeviceInfo => display !== null);
    if (selectedDevices.length !== identities.length || selectedDevices.length === 0) { setError(messages.unavailable); return; }
    const primary = selectedDevices.find((display) => display.isPrimary) ?? selectedDevices[0];
    const layout: DisplayLayoutSnapshot = { id: `${Date.now()}`, capturedAt: new Date().toISOString(), displays: selectedDevices.map((display) => ({ identity: display.identity, isActive: true, isAvailable: display.isAvailable, isPrimary: sameIdentity(primary.identity, display.identity), positionX: display.positionX - primary.positionX, positionY: display.positionY - primary.positionY, width: display.width, height: display.height, orientation: display.orientation, refreshRateNumerator: display.refreshRateNumerator, refreshRateDenominator: display.refreshRateDenominator, refreshRateHz: display.refreshRateHz, currentModeAvailable: display.currentModeAvailable, preferredWidth: display.preferredWidth, preferredHeight: display.preferredHeight, sourceId: display.sourceId, targetId: display.targetId, adapterLuid: display.adapterLuid, sourceDeviceName: display.sourceDeviceName, connectionTechnology: display.connectionTechnology })) };
    const existingProfile = profiles.find((profile) => profile.name.toLowerCase() === trimmedName.toLowerCase()) ?? null;
    const nextProfiles = existingProfile ? profiles.map((profile) => profile.name.toLowerCase() === trimmedName.toLowerCase() ? { ...profile, name: existingProfile.name, layout } : profile) : [...profiles, { name: trimmedName, layout }];
    const resolvedProfileName = existingProfile?.name ?? trimmedName;
    const nextProfileBehaviours = { ...profileBehaviours, [resolvedProfileName]: profileBehaviours[resolvedProfileName] ?? pcGamingBehaviour };
    const nextProfileLaunchers = { ...profileLaunchers, [resolvedProfileName]: profileLaunchers[resolvedProfileName] ?? launcher };
    const nextProfileHotkeys = { ...profileHotkeys, [resolvedProfileName]: existingProfile ? (profileHotkeys[resolvedProfileName] ?? { enterProfile: hotkeys.pcGamingMode }) : { enterProfile: '' } };
    const nextProfileControllerShortcuts = { ...profileControllerShortcuts, [resolvedProfileName]: existingProfile ? (profileControllerShortcuts[resolvedProfileName] ?? null) : null };
    setProfiles(nextProfiles); setActiveProfileName(resolvedProfileName); setLayout(layout); setSelectedProfileName(resolvedProfileName); setProfileBehaviours(nextProfileBehaviours); setProfileLaunchers(nextProfileLaunchers); setProfileHotkeys(nextProfileHotkeys); setProfileControllerShortcuts(nextProfileControllerShortcuts);
    pendingSettingsOverrideRef.current = { pcGamingProfiles: nextProfiles, activePcGamingProfileName: resolvedProfileName, pcGamingLayout: layout, pcGamingProfileBehaviours: nextProfileBehaviours, pcGamingProfileLaunchers: nextProfileLaunchers, pcGamingProfileHotkeys: nextProfileHotkeys, pcGamingProfileControllerShortcuts: nextProfileControllerShortcuts };
    markUnsavedChanges(); setError(null); setMessage(isEditingSection ? messages.prepared(resolvedProfileName) : messages.ready(resolvedProfileName));
  }

  async function commitGamingDisplayDraft(allowOverwrite: boolean, overwriteAction: OverwriteProfileAction) {
    const draft = displayProfileDraftRef.current; const profileName = draft.profileName.trim(); const identities = draft.identities;
    setDisplayValidationState({ showNameError: profileName.length === 0, showSelectionError: identities.length === 0 });
    const hasDraft = profileName.length > 0 && identities.length > 0;
    if ((profileName.length > 0 || identities.length > 0) && !hasDraft || (isCreatingProfileFlow && !hasDraft)) { setError(messages.invalid); return false; }
    if (!hasDraft && profiles.length === 0) { setError(messages.noProfiles); return false; }
    if (identities.length > MAX_PROFILE_DISPLAYS) { setError(PROFILE_DISPLAY_LIMIT_MESSAGE); return false; }
    if (hasDraft) {
      const existingProfile = profiles.find((profile) => profile.name.toLowerCase() === profileName.toLowerCase()) ?? null;
      const isEditingSelectedProfile = selectedProfileName.length > 0 && selectedProfileName.toLowerCase() === profileName.toLowerCase();
      const isLayoutUnchanged = existingProfile ? areIdentitySetsEqual(identities, extractDisplayIdentities(existingProfile.layout)) : false;
      if (existingProfile && !isEditingSelectedProfile && !isLayoutUnchanged && !allowOverwrite) { setPendingOverwriteProfileAction(overwriteAction); setShowOverwriteProfileDialog(true); return false; }
      if (!existingProfile || !isLayoutUnchanged) await saveSelectedPcGamingLayout(profileName, identities);
    }
    setDisplayValidationState({ showNameError: false, showSelectionError: false }); return true;
  }

  return { refreshDisplays, saveSelectedPcGamingLayout, commitGamingDisplayDraft };
}
