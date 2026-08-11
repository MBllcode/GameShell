import type { Dispatch, SetStateAction } from 'react';
import type { AppStatus, DisplayIdentity, DisplayLayoutProfile, DisplayLayoutSnapshot } from '../../../types/bridge';
import type { DashboardPageProps } from '../../../pages/DashboardPage';
import type { PcGamingProfileBehaviourMap, PcGamingProfileControllerShortcutMap, PcGamingProfileHotkeysMap, PcGamingProfileLauncherMap } from '../../profiles/utils';
import type { SettingsPayloadOverrides } from '../../settings/buildSettingsPayload';
import { steps } from '../../setup/setupFlow';
import { useDashboardActions } from './useDashboardActions';
import { useDashboardPageModel } from './useDashboardPageModel';
import { useDashboardSetupActions } from './useDashboardSetupActions';
import type { SetupMode } from '../../setup/setupFlow';
import type { BootstrapRoute } from '../../setup/setupRouting';
import type { PcGamingBehaviourState } from '../../settings/hooks/useSettingsFormState';

type Options = {
  status: AppStatus;
  launcher: DashboardPageProps['launcher'];
  pcGamingLayout: DisplayLayoutSnapshot | null;
  pcGamingProfiles: DisplayLayoutProfile[];
  pcGamingProfileLaunchers: PcGamingProfileLauncherMap;
  pcGamingProfileHotkeys: PcGamingProfileHotkeysMap;
  activePcGamingProfileName: string | null;
  selectedProfileName: string;
  globalWorkModeHotkey: string;
  isModeSwitching: boolean;
  isRemovingProfile: boolean;
  activeModeAction: string | null;
  message: string;
  hasError: boolean;
  hasUnsavedChanges: boolean;
  isSetupComplete: boolean;
  profileBehaviours: PcGamingProfileBehaviourMap;
  profileLaunchers: PcGamingProfileLauncherMap;
  profileHotkeys: PcGamingProfileHotkeysMap;
  profileControllerShortcuts: PcGamingProfileControllerShortcutMap;
  saveCurrentSettings: (isFirstRunCompleted?: boolean, overrides?: SettingsPayloadOverrides) => Promise<unknown>;
  setStatus: Dispatch<SetStateAction<AppStatus>>;
  setActiveModeAction: (value: string | null) => void;
  setIsRemovingProfile: (value: boolean) => void;
  setActiveProfileName: (value: string | null) => void;
  setSelectedProfileName: (value: string) => void;
  setLayout: (value: DisplayLayoutSnapshot | null) => void;
  setError: (value: string | null) => void;
  setMessage: Dispatch<SetStateAction<string>>;
  syncDisplayProfileDraft: (draft: { profileName: string; topology: import('../../../types/bridge').GamingProfileDisplayTopology }) => void;
  setStepIndex: Dispatch<SetStateAction<number>>;
  setSetupMode: Dispatch<SetStateAction<SetupMode>>;
  setBootstrapRoute: Dispatch<SetStateAction<BootstrapRoute>>;
  setEditingProfileId: Dispatch<SetStateAction<string | null>>;
  setHasAdvancedInCreateProfileFlow: Dispatch<SetStateAction<boolean>>;
  setIsSetupComplete: Dispatch<SetStateAction<boolean>>;
  setProfileHotkeyValidationError: Dispatch<SetStateAction<string | null>>;
  setProfileControllerValidationError: Dispatch<SetStateAction<string | null>>;
  setHotkeys: Dispatch<SetStateAction<{ consoleMode: string; pcGamingMode: string; workMode: string }>>;
  setPcGamingBehaviour: Dispatch<SetStateAction<PcGamingBehaviourState>>;
  setShowOverwriteProfileDialog: Dispatch<SetStateAction<boolean>>;
};

export function useDashboardFeature(options: Options) {
  const dashboardActions = useDashboardActions({
    hasUnsavedChanges: options.hasUnsavedChanges,
    isSetupComplete: options.isSetupComplete,
    selectedProfileName: options.selectedProfileName,
    profiles: options.pcGamingProfiles,
    activeProfileName: options.activePcGamingProfileName,
    profileBehaviours: options.profileBehaviours,
    profileLaunchers: options.profileLaunchers,
    profileHotkeys: options.profileHotkeys,
    profileControllerShortcuts: options.profileControllerShortcuts,
    saveCurrentSettings: options.saveCurrentSettings,
    setStatus: options.setStatus,
    setActiveModeAction: options.setActiveModeAction,
    setIsRemovingProfile: options.setIsRemovingProfile,
    setActiveProfileName: options.setActiveProfileName,
    setSelectedProfileName: options.setSelectedProfileName,
    setLayout: options.setLayout,
    setError: options.setError,
    setMessage: options.setMessage
  });
  const setupActions = useDashboardSetupActions({
    steps,
    profiles: options.pcGamingProfiles,
    selectedProfileName: options.selectedProfileName,
    setStepIndex: options.setStepIndex,
    setSetupMode: options.setSetupMode,
    setBootstrapRoute: options.setBootstrapRoute,
    setEditingProfileId: options.setEditingProfileId,
    setHasAdvancedInCreateProfileFlow: options.setHasAdvancedInCreateProfileFlow,
    setIsSetupComplete: options.setIsSetupComplete,
    syncDisplayProfileDraft: options.syncDisplayProfileDraft,
    setProfileHotkeyValidationError: options.setProfileHotkeyValidationError,
    setProfileControllerValidationError: options.setProfileControllerValidationError,
    setHotkeys: options.setHotkeys,
    setPcGamingBehaviour: options.setPcGamingBehaviour,
    setShowOverwriteProfileDialog: options.setShowOverwriteProfileDialog,
    setMessage: options.setMessage
  });

  return useDashboardPageModel({
    state: {
      status: options.status,
      launcher: options.launcher,
      pcGamingLayout: options.pcGamingLayout,
      pcGamingProfiles: options.pcGamingProfiles,
      pcGamingProfileLaunchers: options.pcGamingProfileLaunchers,
      pcGamingProfileHotkeys: options.pcGamingProfileHotkeys,
      activeProfileName: options.activePcGamingProfileName,
      selectedProfileName: options.selectedProfileName,
      globalWorkModeHotkey: options.globalWorkModeHotkey,
      isModeSwitching: options.isModeSwitching,
      isRemovingProfile: options.isRemovingProfile,
      activeModeAction: options.activeModeAction,
      message: options.message,
      hasError: options.hasError
    },
    actions: {
      runModeCommand: dashboardActions.runModeCommand,
      deleteSelectedProfile: dashboardActions.deleteSelectedProfile,
      selectProfile: options.setSelectedProfileName,
      createProfile: setupActions.createProfile,
      editSettings: setupActions.editSettings
    }
  });
}
