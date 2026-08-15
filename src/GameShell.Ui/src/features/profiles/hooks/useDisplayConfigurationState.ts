import { useState } from 'react';
import type { ControllerShortcutSettings, DisplayDeviceInfo, DisplayIdentity, DisplayLayoutProfile, DisplayLayoutSnapshot } from '../../../types/bridge';
import type { GamingProfileBehaviourMap, GamingProfileControllerShortcutMap, GamingProfileHotkeysMap, GamingProfileLauncherMap } from '../utils';

type Options = {
  selectedProfileName: string;
};

export function useDisplayConfigurationState({ selectedProfileName }: Options) {
  const [displays, setDisplays] = useState<DisplayDeviceInfo[]>([]);
  const [isRefreshingDisplays, setIsRefreshingDisplays] = useState(false);
  const [displayListError, setDisplayListError] = useState<string | null>(null);
  const [selectedConsoleDisplay, setSelectedConsoleDisplay] = useState<DisplayIdentity | null>(null);
  const [selectedConsoleDisplays, setSelectedConsoleDisplays] = useState<DisplayIdentity[]>([]);
  const [gamingLayout, setGamingLayout] = useState<DisplayLayoutSnapshot | null>(null);
  const [workModeLayout, setWorkModeLayout] = useState<DisplayLayoutSnapshot | null>(null);
  const [gamingProfiles, setGamingProfiles] = useState<DisplayLayoutProfile[]>([]);
  const [activeGamingProfileName, setActiveGamingProfileName] = useState<string | null>(null);
  const [gamingProfileBehaviours, setGamingProfileBehaviours] = useState<GamingProfileBehaviourMap>({});
  const [gamingProfileLaunchers, setGamingProfileLaunchers] = useState<GamingProfileLauncherMap>({});
  const [gamingProfileHotkeys, setGamingProfileHotkeys] = useState<GamingProfileHotkeysMap>({});
  const [gamingProfileControllerShortcuts, setGamingProfileControllerShortcuts] = useState<GamingProfileControllerShortcutMap>({});
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
    gamingLayout,
    setGamingLayout,
    workModeLayout,
    setWorkModeLayout,
    gamingProfiles,
    setGamingProfiles,
    activeGamingProfileName,
    setActiveGamingProfileName,
    gamingProfileBehaviours,
    setGamingProfileBehaviours,
    gamingProfileLaunchers,
    setGamingProfileLaunchers,
    gamingProfileHotkeys,
    setGamingProfileHotkeys,
    gamingProfileControllerShortcuts,
    setGamingProfileControllerShortcuts,
    workModeControllerShortcut,
    setWorkModeControllerShortcut,
    profileHotkeyValidationError,
    setProfileHotkeyValidationError,
    profileControllerValidationError,
    setProfileControllerValidationError
  };
}
