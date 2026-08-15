import type { Dispatch, SetStateAction } from 'react';
import type { AppStatus, DisplayIdentity, DisplayLayoutProfile, DisplayLayoutSnapshot } from '../../../types/bridge';
import type { DashboardPageProps } from '../../../pages/DashboardPage';
import type { GamingProfileBehaviourMap, GamingProfileControllerShortcutMap, GamingProfileHotkeysMap, GamingProfileLauncherMap } from '../../profiles/utils';
import type { SettingsPayloadOverrides } from '../../settings/buildSettingsPayload';
import { steps } from '../../setup/setupFlow';
import { useDashboardActions } from './useDashboardActions';
import { useDashboardPageModel } from './useDashboardPageModel';
import { useDashboardSetupActions } from './useDashboardSetupActions';
import type { SetupMode } from '../../setup/setupFlow';
import type { BootstrapRoute } from '../../setup/setupRouting';
import type { GamingBehaviourState } from '../../settings/hooks/useSettingsFormState';

type Options = {
  status: AppStatus;
  launcher: DashboardPageProps['launcher'];
  gamingLayout: DisplayLayoutSnapshot | null;
  gamingProfiles: DisplayLayoutProfile[];
  gamingProfileLaunchers: GamingProfileLauncherMap;
  gamingProfileHotkeys: GamingProfileHotkeysMap;
  activeGamingProfileName: string | null;
  selectedProfileName: string;
  globalWorkModeHotkey: string;
  isModeSwitching: boolean;
  isRemovingProfile: boolean;
  activeModeAction: string | null;
  message: string;
  hasError: boolean;
  hasUnsavedChanges: boolean;
  isSetupComplete: boolean;
  profileBehaviours: GamingProfileBehaviourMap;
  profileLaunchers: GamingProfileLauncherMap;
  profileHotkeys: GamingProfileHotkeysMap;
  profileControllerShortcuts: GamingProfileControllerShortcutMap;
  saveCurrentSettings: (isFirstRunCompleted?: boolean, overrides?: SettingsPayloadOverrides) => Promise<unknown>;
  setStatus: Dispatch<SetStateAction<AppStatus>>;
  setActiveModeAction: (value: string | null) => void;
  setIsRemovingProfile: (value: boolean) => void;
  setActiveProfileName: (value: string | null) => void;
  setSelectedProfileName: (value: string) => void;
  setLayout: (value: DisplayLayoutSnapshot | null) => void;
  setError: (value: string | null) => void;
  setMessage: Dispatch<SetStateAction<string>>;
  setTransientMessage: (value: string, durationMs?: number) => void;
  syncDisplayProfileDraft: (draft: { profileName: string; topology: import('../../../types/bridge').GamingProfileDisplayTopology }) => void;
  setStepIndex: Dispatch<SetStateAction<number>>;
  setSetupMode: Dispatch<SetStateAction<SetupMode>>;
  setBootstrapRoute: Dispatch<SetStateAction<BootstrapRoute>>;
  setEditingProfileId: Dispatch<SetStateAction<string | null>>;
  setHasAdvancedInCreateProfileFlow: Dispatch<SetStateAction<boolean>>;
  setHasUnsavedChanges: Dispatch<SetStateAction<boolean>>;
  setIsSetupComplete: Dispatch<SetStateAction<boolean>>;
  setProfileHotkeyValidationError: Dispatch<SetStateAction<string | null>>;
  setProfileControllerValidationError: Dispatch<SetStateAction<string | null>>;
  setHotkeys: Dispatch<SetStateAction<{ consoleMode: string; gamingMode: string; workMode: string }>>;
  setGamingBehaviour: Dispatch<SetStateAction<GamingBehaviourState>>;
  setShowOverwriteProfileDialog: Dispatch<SetStateAction<boolean>>;
};

export function useDashboardFeature(options: Options) {
  const dashboardActions = useDashboardActions({
    hasUnsavedChanges: options.hasUnsavedChanges,
    isSetupComplete: options.isSetupComplete,
    selectedProfileName: options.selectedProfileName,
    profiles: options.gamingProfiles,
    activeProfileName: options.activeGamingProfileName,
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
    setMessage: options.setMessage,
    setTransientMessage: options.setTransientMessage
  });
  const setupActions = useDashboardSetupActions({
    steps,
    profiles: options.gamingProfiles,
    selectedProfileName: options.selectedProfileName,
    setStepIndex: options.setStepIndex,
    setSetupMode: options.setSetupMode,
    setBootstrapRoute: options.setBootstrapRoute,
    setEditingProfileId: options.setEditingProfileId,
    setHasAdvancedInCreateProfileFlow: options.setHasAdvancedInCreateProfileFlow,
    setHasUnsavedChanges: options.setHasUnsavedChanges,
    setIsSetupComplete: options.setIsSetupComplete,
    syncDisplayProfileDraft: options.syncDisplayProfileDraft,
    setProfileHotkeyValidationError: options.setProfileHotkeyValidationError,
    setProfileControllerValidationError: options.setProfileControllerValidationError,
    setHotkeys: options.setHotkeys,
    setGamingBehaviour: options.setGamingBehaviour,
    setShowOverwriteProfileDialog: options.setShowOverwriteProfileDialog,
    setMessage: options.setMessage
  });

  return useDashboardPageModel({
    state: {
      status: options.status,
      launcher: options.launcher,
      gamingLayout: options.gamingLayout,
      gamingProfiles: options.gamingProfiles,
      gamingProfileLaunchers: options.gamingProfileLaunchers,
      gamingProfileHotkeys: options.gamingProfileHotkeys,
      activeProfileName: options.activeGamingProfileName,
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
