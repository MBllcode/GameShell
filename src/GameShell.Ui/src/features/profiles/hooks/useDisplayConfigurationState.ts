import { useState } from 'react';
import type { ControllerShortcutSettings, DisplayDeviceInfo, DisplayIdentity, DisplayLayoutProfile, DisplayLayoutSnapshot } from '../../../types/bridge';
import type { PcGamingProfileBehaviourMap, PcGamingProfileControllerShortcutMap, PcGamingProfileHotkeysMap, PcGamingProfileLauncherMap } from '../utils';

type Options = {
  selectedProfileName: string;
};

export function useDisplayConfigurationState({ selectedProfileName }: Options) {
  const [displays, setDisplays] = useState<DisplayDeviceInfo[]>([]);
  const [isRefreshingDisplays, setIsRefreshingDisplays] = useState(false);
  const [displayListError, setDisplayListError] = useState<string | null>(null);
  const [selectedConsoleDisplay, setSelectedConsoleDisplay] = useState<DisplayIdentity | null>(null);
  const [selectedConsoleDisplays, setSelectedConsoleDisplays] = useState<DisplayIdentity[]>([]);
  const [pcGamingLayout, setPcGamingLayout] = useState<DisplayLayoutSnapshot | null>(null);
  const [pcGamingProfiles, setPcGamingProfiles] = useState<DisplayLayoutProfile[]>([]);
  const [activePcGamingProfileName, setActivePcGamingProfileName] = useState<string | null>(null);
  const [pcGamingProfileBehaviours, setPcGamingProfileBehaviours] = useState<PcGamingProfileBehaviourMap>({});
  const [pcGamingProfileLaunchers, setPcGamingProfileLaunchers] = useState<PcGamingProfileLauncherMap>({});
  const [pcGamingProfileHotkeys, setPcGamingProfileHotkeys] = useState<PcGamingProfileHotkeysMap>({});
  const [pcGamingProfileControllerShortcuts, setPcGamingProfileControllerShortcuts] = useState<PcGamingProfileControllerShortcutMap>({});
  const [workModeControllerShortcut, setWorkModeControllerShortcut] = useState<ControllerShortcutSettings | null>(null);
  const [profileHotkeyValidationError, setProfileHotkeyValidationError] = useState<string | null>(null);
  const [profileControllerValidationError, setProfileControllerValidationError] = useState<string | null>(null);

  return {
    selectedProfileName,
    displays,
    setDisplays,
    isRefreshingDisplays,
    setIsRefreshingDisplays,
    displayListError,
    setDisplayListError,
    selectedConsoleDisplay,
    setSelectedConsoleDisplay,
    selectedConsoleDisplays,
    setSelectedConsoleDisplays,
    pcGamingLayout,
    setPcGamingLayout,
    pcGamingProfiles,
    setPcGamingProfiles,
    activePcGamingProfileName,
    setActivePcGamingProfileName,
    pcGamingProfileBehaviours,
    setPcGamingProfileBehaviours,
    pcGamingProfileLaunchers,
    setPcGamingProfileLaunchers,
    pcGamingProfileHotkeys,
    setPcGamingProfileHotkeys,
    pcGamingProfileControllerShortcuts,
    setPcGamingProfileControllerShortcuts,
    workModeControllerShortcut,
    setWorkModeControllerShortcut,
    profileHotkeyValidationError,
    setProfileHotkeyValidationError,
    profileControllerValidationError,
    setProfileControllerValidationError
  };
}
