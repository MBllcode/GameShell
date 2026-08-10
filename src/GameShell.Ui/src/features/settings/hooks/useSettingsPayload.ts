import { buildSettingsPayload, type BuildSettingsPayloadState, type SettingsPayloadOverrides } from '../buildSettingsPayload';
import type { AppSettings } from '../../../types/bridge';

type Options = BuildSettingsPayloadState & { settings: AppSettings | null };

export function useSettingsPayload(options: Options) {
  return (isFirstRunCompleted = true, overrides?: SettingsPayloadOverrides): AppSettings => {
    return buildSettingsPayload(options, isFirstRunCompleted, overrides);
  };
}
