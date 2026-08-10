import type { Dispatch, SetStateAction } from 'react';
import type { LauncherType } from '../../../utils/modes';

type WorkBehaviour = {
  closeLauncher: boolean;
  showTaskbars: boolean;
  showDesktopIcons: boolean;
  doNotMinimizeWindows: boolean;
};

type FinishOptions = {
  startWithWindows: boolean;
  startMinimized: boolean;
  startMenuShortcut: boolean;
  desktopShortcut: boolean;
};

type Options = {
  selectedProfileName: string;
  setLauncher: Dispatch<SetStateAction<LauncherType>>;
  setWorkBehaviour: Dispatch<SetStateAction<WorkBehaviour>>;
  setFinishOptions: Dispatch<SetStateAction<FinishOptions>>;
  markUnsavedChanges: () => void;
  setProfileLauncher: (profileName: string, launcher: LauncherType) => void;
  updateProfileHotkey: (profileName: string, key: 'enterProfile', value: string) => void;
  updateProfileControllerShortcut: (profileName: string, buttons: string[]) => void;
  updateWorkModeHotkey: (value: string) => void;
  updateWorkModeControllerShortcut: (buttons: string[]) => void;
  startControllerCapture: (profileId: string) => Promise<void>;
  cancelControllerCapture: () => Promise<void>;
};

export function useSetupPageActions(options: Options) {
  const updateProfileLauncher = (nextLauncher: LauncherType) => {
    if (!options.selectedProfileName) {
      options.setLauncher(nextLauncher);
      options.markUnsavedChanges();
      return;
    }
    options.setProfileLauncher(options.selectedProfileName, nextLauncher);
  };

  return {
    updateProfileLauncher,
    setWorkBehaviour: (behaviour: WorkBehaviour) => {
      options.setWorkBehaviour(behaviour);
      options.markUnsavedChanges();
    },
    setFinishOptions: (finishOptions: FinishOptions) => {
      options.setFinishOptions(finishOptions);
      options.markUnsavedChanges();
    },
    startControllerCapture: (profileId: string) => { void options.startControllerCapture(profileId); },
    cancelControllerCapture: () => { void options.cancelControllerCapture(); },
    removeWorkControllerShortcut: () => options.updateWorkModeControllerShortcut([]),
    removeProfileControllerShortcut: () => options.updateProfileControllerShortcut(options.selectedProfileName, []),
    removeWorkHotkey: () => options.updateWorkModeHotkey(''),
    removeProfileHotkey: () => options.updateProfileHotkey(options.selectedProfileName, 'enterProfile', ''),
    changeProfileHotkey: (value: string) => options.updateProfileHotkey(options.selectedProfileName, 'enterProfile', value),
    updateProfileHotkey: (value: string) => options.updateProfileHotkey(options.selectedProfileName, 'enterProfile', value),
    updateProfileControllerShortcut: (buttons: string[]) => options.updateProfileControllerShortcut(options.selectedProfileName, buttons)
  };
}
