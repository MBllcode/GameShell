import type { useSettingsFormState } from './useSettingsFormState';
import type { useDisplayConfigurationState } from '../../profiles/hooks/useDisplayConfigurationState';
import type { useSetupWorkflowState } from '../../setup/hooks/useSetupWorkflowState';
import type { useAppRuntimeState } from '../../../app/hooks/useAppRuntimeState';
import { useGameShellBridge } from '../../../bridge/useGameShellBridge';
import type { ControllerStatusInfo } from '../../../bridge/contracts';
import { hydrateSettings } from '../hydrateSettings';
import type { AppSettings } from '../../../bridge/contracts';
import { buildSettingsPayload as createSettingsPayload } from '../buildSettingsPayload';
import { useSettingsPersistence } from './useSettingsPersistence';
import type { SettingsPayloadOverrides } from '../buildSettingsPayload';
import { assertSetupStateInvariants, resolveBootstrapState } from '../../setup/setupRouting';
import { steps } from '../../setup/setupFlow';

type Options = {
  form: ReturnType<typeof useSettingsFormState>;
  display: ReturnType<typeof useDisplayConfigurationState>;
  setup: ReturnType<typeof useSetupWorkflowState>;
  runtime: ReturnType<typeof useAppRuntimeState>;
  preferredProfileName: () => string;
  setSelectedProfileName: (name: string) => void;
  applyControllerStatus: (status: ControllerStatusInfo) => void;
};

export function useSettingsIntegrationFeature(input: Options) {
  const options = {
    ...input.form, ...input.display, ...input.setup, ...input.runtime,
    preferredProfileName: input.preferredProfileName,
    setSelectedProfileName: input.setSelectedProfileName,
    applyControllerStatus: input.applyControllerStatus,
    setProfiles: input.display.setGamingProfiles,
    setActiveProfileName: input.display.setActiveGamingProfileName,
    setLayout: input.display.setGamingLayout,
    setDisplaySettling: input.display.setIsDisplaySettling,
    setInitialLoading: input.setup.setIsInitialLoading
  };
  const applySettingsToState = (settings: AppSettings) => hydrateSettings(settings, options);

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
    onDisplayDiscoveryState: (state) => {
      options.setDisplaySettling(state === 'Settling');
      if (state === 'Settling') {
        options.setIsRefreshingDisplays(true);
      } else if (state === 'Ready' || state === 'Error') {
        options.setIsRefreshingDisplays(false);
      }
    },
    onControllerStatus: options.applyControllerStatus,
    onError: options.setError,
    onInitialLoadingChange: options.setInitialLoading
    ,onHydrationComplete: () => options.setIsHydrated(true)
  });

  const buildSettingsPayload = (isFirstRunCompleted = true, overrides?: SettingsPayloadOverrides) =>
    createSettingsPayload(options, isFirstRunCompleted, overrides);
  const persistence = useSettingsPersistence({
    isSetupComplete: options.isSetupComplete,
    settings: options.settings,
    setupMode: options.setupMode,
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
    hasUnsavedChanges: options.hasUnsavedChanges,
    setHasUnsavedChanges: options.setHasUnsavedChanges,
    setIsSaving: options.setIsSaving,
    setError: options.setError,
    setMessage: options.setMessage
  });

  return { applySettingsToState, buildSettingsPayload, ...persistence };
}
