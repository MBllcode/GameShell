import type { DashboardPageProps } from '../../../pages/DashboardPage';

type Options = {
  state: Pick<DashboardPageProps, 'status' | 'launcher' | 'gamingLayout' | 'gamingProfiles' | 'gamingProfileLaunchers' | 'gamingProfileHotkeys' | 'activeProfileName' | 'selectedProfileName' | 'globalWorkModeHotkey' | 'isModeSwitching' | 'isRemovingProfile' | 'message' | 'hasError'>;
  actions: Pick<DashboardPageProps, 'runModeCommand' | 'deleteSelectedProfile' | 'selectProfile' | 'createProfile' | 'editSettings'>;
};

export function useDashboardPageModel({ state, actions }: Options): DashboardPageProps {
  return { ...state, ...actions };
}
