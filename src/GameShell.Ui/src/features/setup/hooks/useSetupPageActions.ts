import type { Dispatch, SetStateAction } from 'react';
import type { LauncherType } from '../../../utils/modes';
import type { FinishOptions } from '../../settings/hooks/useSettingsFormState';

type WorkBehaviour = {
  closeLauncher: boolean;
  showTaskbars: boolean;
  showDesktopIcons: boolean;
  doNotMinimizeWindows: boolean;
};

type Options = {
  workingProfileName: string;
  setLauncher: Dispatch<SetStateAction<LauncherType>>;
  setWorkBehaviour: Dispatch<SetStateAction<WorkBehaviour>>;
  setFinishOptions: Dispatch<SetStateAction<FinishOptions>>;
  markUnsavedChanges: () => void;
  setProfileLauncher: (profileName: string, launcher: LauncherType) => void;
  updateProfileHotkey: (profileName: string, key: 'enterProfile', value: string) => boolean;
  updateProfileControllerShortcut: (profileName: string, buttons: string[]) => boolean;
  updateWorkModeHotkey: (value: string) => boolean;
  updateWorkModeControllerShortcut: (buttons: string[]) => boolean;
  startControllerCapture: (profileId: string) => Promise<void>;
  cancelControllerCapture: () => Promise<void>;
};

export function useSetupPageActions(options: Options) {
  const updateProfileLauncher = (nextLauncher: LauncherType) => {
    if (!options.workingProfileName) {
      options.setLauncher(nextLauncher);
      options.markUnsavedChanges();
      return;
    }
    options.setProfileLauncher(options.workingProfileName, nextLauncher);
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
    removeProfileControllerShortcut: () => options.updateProfileControllerShortcut(options.workingProfileName, []),
    removeWorkHotkey: () => options.updateWorkModeHotkey(''),
    removeProfileHotkey: () => options.updateProfileHotkey(options.workingProfileName, 'enterProfile', ''),
    changeProfileHotkey: (value: string) => options.updateProfileHotkey(options.workingProfileName, 'enterProfile', value),
    updateProfileHotkey: (value: string) => options.updateProfileHotkey(options.workingProfileName, 'enterProfile', value),
    updateProfileControllerShortcut: (buttons: string[]) => options.updateProfileControllerShortcut(options.workingProfileName, buttons)
  };
}
