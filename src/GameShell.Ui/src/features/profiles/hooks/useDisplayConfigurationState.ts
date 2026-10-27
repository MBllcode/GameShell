import { useMemo, useState } from 'react';
import type { ControllerShortcutSettings, DisplayDeviceInfo, DisplayIdentity, DisplayLayoutSnapshot } from '../../../bridge/contracts';
import { profileViews, type Profile } from '../profileModel';

type Options = {
  selectedProfileName: string;
};

export function useDisplayConfigurationState({ selectedProfileName }: Options) {
  const [displays, setDisplays] = useState<DisplayDeviceInfo[]>([]);
  const [isRefreshingDisplays, setIsRefreshingDisplays] = useState(false);
  const [isDisplaySettling, setIsDisplaySettling] = useState(false);
  const [displayListError, setDisplayListError] = useState<string | null>(null);
  const [selectedConsoleDisplay, setSelectedConsoleDisplay] = useState<DisplayIdentity | null>(null);
  const [selectedConsoleDisplays, setSelectedConsoleDisplays] = useState<DisplayIdentity[]>([]);
  const [gamingLayout, setGamingLayout] = useState<DisplayLayoutSnapshot | null>(null);
  const [workModeLayout, setWorkModeLayout] = useState<DisplayLayoutSnapshot | null>(null);
  const [gamingProfiles, setGamingProfiles] = useState<Profile[]>([]);
  const [activeGamingProfileName, setActiveGamingProfileName] = useState<string | null>(null);
  const [workModeControllerShortcut, setWorkModeControllerShortcut] = useState<ControllerShortcutSettings | null>(null);
  const [profileHotkeyValidationError, setProfileHotkeyValidationError] = useState<string | null>(null);
  const [profileControllerValidationError, setProfileControllerValidationError] = useState<string | null>(null);

  const views = useMemo(() => profileViews(gamingProfiles), [gamingProfiles]);

  return {
    ...views,
    selectedProfileName,
    displays,
    setDisplays,
    isRefreshingDisplays,
    setIsRefreshingDisplays,
    isDisplaySettling,
    setIsDisplaySettling,
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
    workModeControllerShortcut,
    setWorkModeControllerShortcut,
    profileHotkeyValidationError,
    setProfileHotkeyValidationError,
    profileControllerValidationError,
    setProfileControllerValidationError
  };
}
