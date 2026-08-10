import { sendBridgeCommand } from '../../../services/webViewBridge';
import type { Dispatch, SetStateAction } from 'react';
import type { AppSettings, AppStatus } from '../../../types/bridge';
import type { SettingsPayloadOverrides } from '../buildSettingsPayload';

type UseSettingsPersistenceOptions = {
  isSetupComplete: boolean;
  isEditingSection: boolean;
  pcGamingProfilesCount: number;
  preferredProfileName: () => string;
  pendingSettingsOverrideRef: { current: SettingsPayloadOverrides | null };
  buildSettingsPayload: (isFirstRunCompleted: boolean, overrides?: SettingsPayloadOverrides) => AppSettings;
  applySettingsToState: (settings: AppSettings) => void;
  setSettings: (settings: AppSettings) => void;
  setStatus: Dispatch<SetStateAction<AppStatus>>;
  setSelectedProfileName: (profileName: string) => void;
  setIsEditingSection: (value: boolean) => void;
  setIsCreatingProfileFlow: (value: boolean) => void;
  setIsSetupComplete: (value: boolean) => void;
  setIsSaving: (value: boolean) => void;
  setError: (value: string | null) => void;
  setMessage: (value: string) => void;
};

export function useSettingsPersistence({
  isSetupComplete,
  isEditingSection,
  pcGamingProfilesCount,
  preferredProfileName,
  pendingSettingsOverrideRef,
  buildSettingsPayload,
  applySettingsToState,
  setSettings,
  setStatus,
  setSelectedProfileName,
  setIsEditingSection,
  setIsCreatingProfileFlow,
  setIsSetupComplete,
  setIsSaving,
  setError,
  setMessage
}: UseSettingsPersistenceOptions) {
  async function saveCurrentSettings(isFirstRunCompleted = isSetupComplete, overrides?: SettingsPayloadOverrides) {
    const savedSettings = await sendBridgeCommand<AppSettings>('saveSettings', buildSettingsPayload(isFirstRunCompleted, overrides));
    setSettings(savedSettings);
    applySettingsToState(savedSettings);
    setStatus((current) => ({
      ...current,
      selectedConsoleDisplay: savedSettings.display.consoleDisplay?.friendlyName ?? null,
      hasPcGamingLayout: Boolean(savedSettings.display.pcGamingLayout) || (savedSettings.display.pcGamingProfiles?.length ?? 0) > 0,
      activeLauncher: savedSettings.launcher.type
    }));
    return savedSettings;
  }

  async function persistAndReturnToDashboard() {
    setError(null);

    if (pcGamingProfilesCount === 0) {
      setError('Najpierw dodaj co najmniej jeden profil ekranów.');
      return;
    }

    setIsSaving(true);
    setMessage(isEditingSection ? 'Zapisywanie zmian...' : 'Zapisywanie ustawień...');

    try {
      const preferredName = preferredProfileName();
      const savedSettings = await saveCurrentSettings(true, pendingSettingsOverrideRef.current ?? undefined);
      pendingSettingsOverrideRef.current = null;
      const savedProfiles = savedSettings.display?.pcGamingProfiles ?? [];
      const matchedPreferred = preferredName
        ? savedProfiles.find((profile) => profile.name.toLowerCase() === preferredName.toLowerCase())?.name ?? null
        : null;
      if (matchedPreferred) {
        setSelectedProfileName(matchedPreferred);
      }
      setIsEditingSection(false);
      setIsCreatingProfileFlow(false);
      setIsSetupComplete(savedSettings.ui.isFirstRunCompleted);
      setMessage('Zapisano zmiany.');
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Nie udało się zapisać zmian.');
    } finally {
      setIsSaving(false);
    }
  }

  return { saveCurrentSettings, persistAndReturnToDashboard };
}
