import type { DashboardPageProps } from '../../../pages/DashboardPage';

type Options = {
  state: Pick<DashboardPageProps, 'status' | 'launcher' | 'pcGamingLayout' | 'pcGamingProfiles' | 'pcGamingProfileLaunchers' | 'pcGamingProfileHotkeys' | 'activeProfileName' | 'selectedProfileName' | 'globalWorkModeHotkey' | 'isModeSwitching' | 'isRemovingProfile' | 'activeModeAction' | 'message' | 'hasError'>;
  actions: Pick<DashboardPageProps, 'runModeCommand' | 'deleteSelectedProfile' | 'selectProfile' | 'createProfile' | 'editSettings'>;
};

export function useDashboardPageModel({ state, actions }: Options): DashboardPageProps {
  return { ...state, ...actions };
}
