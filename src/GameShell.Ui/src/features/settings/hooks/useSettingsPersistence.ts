import { useTranslation } from 'react-i18next';
import { sendBridgeCommand } from '../../../services/webViewBridge';
import type { Dispatch, SetStateAction } from 'react';
import type { AppSettings, AppStatus } from '../../../types/bridge';
import type { SettingsPayloadOverrides } from '../buildSettingsPayload';
import type { SetupMode } from '../../setup/setupFlow';
import { resolvePersistedCompletion, resolvePostSaveSetupComplete, resolveSaveCompletion } from '../../setup/setupRouting';
import type { BootstrapRoute } from '../../setup/setupRouting';

type UseSettingsPersistenceOptions = {
  settings: AppSettings | null;
  isSetupComplete: boolean;
  setupMode: SetupMode;
  bootstrapRoute: BootstrapRoute;
  pcGamingProfilesCount: number;
  preferredProfileName: () => string;
  pendingSettingsOverrideRef: { current: SettingsPayloadOverrides | null };
  buildSettingsPayload: (isFirstRunCompleted: boolean, overrides?: SettingsPayloadOverrides) => AppSettings;
  applySettingsToState: (settings: AppSettings) => void;
  setSettings: (settings: AppSettings) => void;
  setStatus: Dispatch<SetStateAction<AppStatus>>;
  setSelectedProfileName: (profileName: string) => void;
  setSetupMode: (value: SetupMode) => void;
  setBootstrapRoute: (value: BootstrapRoute) => void;
  setEditingProfileId: (value: string | null) => void;
  setIsSetupComplete: (value: boolean) => void;
  setIsSaving: (value: boolean) => void;
  setError: (value: string | null) => void;
  setMessage: (value: string) => void;
};

export function useSettingsPersistence({
  settings,
  isSetupComplete,
  setupMode,
  bootstrapRoute,
  pcGamingProfilesCount,
  preferredProfileName,
  pendingSettingsOverrideRef,
  buildSettingsPayload,
  applySettingsToState,
  setSettings,
  setStatus,
  setSelectedProfileName,
  setSetupMode,
  setBootstrapRoute,
  setEditingProfileId,
  setIsSetupComplete,
  setIsSaving,
  setError,
  setMessage
}: UseSettingsPersistenceOptions) {
  const { t } = useTranslation();

  async function saveCurrentSettings(isFirstRunCompleted = settings?.ui?.isFirstRunCompleted ?? false, overrides?: SettingsPayloadOverrides) {
    const completion = resolvePersistedCompletion(setupMode, settings?.ui?.isFirstRunCompleted ?? false, isFirstRunCompleted);
    const savedSettings = await sendBridgeCommand<AppSettings>('saveSettings', buildSettingsPayload(completion, overrides));
    setSettings(savedSettings);
    applySettingsToState(savedSettings);
    setStatus((current) => ({
      ...current,
      selectedConsoleDisplay: savedSettings.display.consoleDisplay?.friendlyName ?? null,
      hasPcGamingLayout: Boolean(savedSettings.display.pcGamingLayout) || (savedSettings.display.pcGamingProfiles?.length ?? 0) > 0,
      activeLauncher: current.currentMode === 'Work' ? null : savedSettings.launcher.type
    }));
    return savedSettings;
  }

  async function persistAndReturnToDashboard() {
    setError(null);

    if (pcGamingProfilesCount === 0) {
      setError(t('setup.requireProfile'));
      return;
    }

    setIsSaving(true);
    setMessage(setupMode === 'EditProfile' ? t('setup.savingChanges') : t('setup.savingSettings'));

    try {
      const preferredName = preferredProfileName();
      const savedSettings = await saveCurrentSettings(resolveSaveCompletion(setupMode, isSetupComplete), pendingSettingsOverrideRef.current ?? undefined);
      pendingSettingsOverrideRef.current = null;
      const savedProfiles = savedSettings.display?.pcGamingProfiles ?? [];
      const matchedPreferred = preferredName
        ? savedProfiles.find((profile) => profile.name.toLowerCase() === preferredName.toLowerCase())?.name ?? null
        : null;
      if (matchedPreferred) {
        setSelectedProfileName(matchedPreferred);
      }
      setSetupMode('None');
      setBootstrapRoute('Dashboard');
      setEditingProfileId(null);
      setIsSetupComplete(resolvePostSaveSetupComplete(setupMode, savedSettings.ui.isFirstRunCompleted));
      setMessage(t('setup.savedChanges'));
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : t('setup.saveFailed'));
    } finally {
      setIsSaving(false);
    }
  }

  return { saveCurrentSettings, persistAndReturnToDashboard };
}
