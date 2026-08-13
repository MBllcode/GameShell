import type { Dispatch, SetStateAction } from 'react';
import { useGameShellBridge } from '../../../bridge/useGameShellBridge';
import type { AppSettings, AppStatus, ControllerStatusInfo, DisplayDeviceInfo, DisplayIdentity } from '../../../types/bridge';
import { useSettingsHydration } from './useSettingsHydration';
import { useSettingsPayload } from './useSettingsPayload';
import { useSettingsPersistence } from './useSettingsPersistence';
import type { SettingsPayloadOverrides, BuildSettingsPayloadState } from '../buildSettingsPayload';
import type { GamingProfileBehaviourMap, GamingProfileControllerShortcutMap, GamingProfileHotkeysMap, GamingProfileLauncherMap } from '../../profiles/utils';
import type { LauncherType } from '../../../utils/modes';
import type { SetupMode } from '../../setup/setupFlow';
import { assertSetupStateInvariants, resolveBootstrapState, type BootstrapRoute } from '../../setup/setupRouting';
import { steps } from '../../setup/setupFlow';
import type { SupportedLanguage } from '../../../i18n/language';

type Options = BuildSettingsPayloadState & {
  isSetupComplete: boolean;
  setupMode: SetupMode;
  bootstrapRoute: BootstrapRoute;
  gamingProfilesCount: number;
  preferredProfileName: () => string;
  pendingSettingsOverrideRef: { current: SettingsPayloadOverrides | null };
  setSelectedConsoleDisplays: (value: DisplayIdentity[]) => void;
  setSelectedConsoleDisplay: (value: DisplayIdentity | null) => void;
  setProfiles: (value: NonNullable<AppSettings['display']['gamingProfiles']>) => void;
  setActiveProfileName: (value: string | null) => void;
  setProfileBehaviours: (value: GamingProfileBehaviourMap) => void;
  setProfileLaunchers: (value: GamingProfileLauncherMap) => void;
  setProfileHotkeys: (value: GamingProfileHotkeysMap) => void;
  setProfileControllerShortcuts: (value: GamingProfileControllerShortcutMap) => void;
  setWorkModeControllerShortcut: (value: AppSettings['controller']['workMode']) => void;
  setSelectedProfileName: (value: string) => void;
  setLayout: (value: AppSettings['display']['gamingLayout']) => void;
  setHotkeys: (value: BuildSettingsPayloadState['hotkeys']) => void;
  setLauncher: (value: LauncherType) => void;
  setLanguage: (value: SupportedLanguage) => void;
  setConsoleBehaviour: (value: BuildSettingsPayloadState['consoleBehaviour']) => void;
  setGamingBehaviour: (value: BuildSettingsPayloadState['gamingBehaviour']) => void;
  setWorkBehaviour: (value: BuildSettingsPayloadState['workBehaviour']) => void;
  setFinishOptions: (value: BuildSettingsPayloadState['finishOptions']) => void;
  setSetupMode: (value: SetupMode) => void;
  setEditingProfileId: (value: string | null) => void;
  setIsSetupComplete: (value: boolean) => void;
  setHasUnsavedChanges: (value: boolean) => void;
  setSettings: (value: AppSettings) => void;
  setStatus: Dispatch<SetStateAction<AppStatus>>;
  setIsHydrated: (value: boolean) => void;
  setBootstrapRoute: (value: BootstrapRoute) => void;
  setStepIndex: (value: number) => void;
  setIsSaving: (value: boolean) => void;
  setError: (value: string | null) => void;
  setMessage: (value: string) => void;
  applyControllerStatus: (value: ControllerStatusInfo) => void;
  setDisplays: (value: DisplayDeviceInfo[]) => void;
  setDisplayListError: (value: string) => void;
  setInitialLoading: (value: boolean) => void;
};

export function useSettingsIntegrationFeature(options: Options) {
  const applySettingsToState = useSettingsHydration({
    setSelectedConsoleDisplays: options.setSelectedConsoleDisplays,
    setSelectedConsoleDisplay: options.setSelectedConsoleDisplay,
    setProfiles: options.setProfiles,
    setActiveProfileName: options.setActiveProfileName,
    setProfileBehaviours: options.setProfileBehaviours,
    setProfileLaunchers: options.setProfileLaunchers,
    setProfileHotkeys: options.setProfileHotkeys,
    setProfileControllerShortcuts: options.setProfileControllerShortcuts,
    setWorkModeControllerShortcut: options.setWorkModeControllerShortcut,
    setSelectedProfileName: options.setSelectedProfileName,
    setLayout: options.setLayout,
    setHotkeys: options.setHotkeys,
    setLauncher: options.setLauncher,
    setLanguage: options.setLanguage,
    setConsoleBehaviour: options.setConsoleBehaviour,
    setGamingBehaviour: options.setGamingBehaviour,
    setWorkBehaviour: options.setWorkBehaviour,
    setFinishOptions: options.setFinishOptions,
    setHasUnsavedChanges: options.setHasUnsavedChanges,
    pendingSettingsOverrideRef: options.pendingSettingsOverrideRef
  });

  useGameShellBridge({
    onStatus: options.setStatus,
    onSettings: (settings) => {
      options.setSettings(settings);
      applySettingsToState(settings);
      const bootstrap = resolveBootstrapState(true, settings.ui?.isFirstRunCompleted ?? false);
      assertSetupStateInvariants(bootstrap.route, bootstrap.setupMode, bootstrap.editingProfileId);
      options.setBootstrapRoute(bootstrap.route);
      options.setSetupMode(bootstrap.setupMode);
      options.setEditingProfileId(bootstrap.editingProfileId);
      options.setIsSetupComplete(bootstrap.isSetupComplete);
      options.setStepIndex(bootstrap.currentStepIndex);
      const profiles = settings.display?.gamingProfiles ?? [];
      const selectedProfileId = settings.display?.activeGamingProfileName ?? profiles[0]?.name ?? null;
      console.info('BOOTSTRAP STATE', {
        isFirstRunCompleted: settings.ui?.isFirstRunCompleted ?? false,
        profilesCount: profiles.length,
        selectedProfileId: bootstrap.setupMode === 'FirstRun' ? null : selectedProfileId,
        editingProfileId: bootstrap.editingProfileId,
        setupMode: bootstrap.setupMode,
        bootstrapRoute: bootstrap.route,
        isSetupComplete: bootstrap.isSetupComplete,
        currentStep: steps[bootstrap.currentStepIndex]?.id ?? null,
        profileDraftName: bootstrap.profileDraftName
      });
    },
    onDisplays: options.setDisplays,
    onDisplayError: options.setDisplayListError,
    onControllerStatus: options.applyControllerStatus,
    onError: options.setError,
    onInitialLoadingChange: options.setInitialLoading
    ,onHydrationComplete: () => options.setIsHydrated(true)
  });

  const buildSettingsPayload = useSettingsPayload(options);
  const persistence = useSettingsPersistence({
    isSetupComplete: options.isSetupComplete,
    settings: options.settings,
    setupMode: options.setupMode,
    bootstrapRoute: options.bootstrapRoute,
    gamingProfilesCount: options.gamingProfilesCount,
    preferredProfileName: options.preferredProfileName,
    pendingSettingsOverrideRef: options.pendingSettingsOverrideRef,
    buildSettingsPayload,
    applySettingsToState,
    setSettings: options.setSettings,
    setStatus: options.setStatus,
    setSelectedProfileName: options.setSelectedProfileName,
    setSetupMode: options.setSetupMode,
    setBootstrapRoute: options.setBootstrapRoute,
    setEditingProfileId: options.setEditingProfileId,
    setIsSetupComplete: options.setIsSetupComplete,
    setIsSaving: options.setIsSaving,
    setError: options.setError,
    setMessage: options.setMessage
  });

  return { applySettingsToState, buildSettingsPayload, ...persistence };
}