import type { SetupMode } from './setupFlow';

export type InitialRoute = 'Loading' | 'Dashboard' | 'FirstRun';
export type BootstrapRoute = InitialRoute | 'ProfileSetup';

export type BootstrapState = {
  route: BootstrapRoute;
  setupMode: SetupMode;
  editingProfileId: string | null;
  isSetupComplete: boolean;
  currentStepIndex: number;
  profileDraftName: string;
};

export function resolveBootstrapState(isHydrated: boolean, isFirstRunCompleted: boolean): BootstrapState {
  if (!isHydrated) {
    return { route: 'Loading', setupMode: 'None', editingProfileId: null, isSetupComplete: false, currentStepIndex: 0, profileDraftName: '' };
  }

  if (isFirstRunCompleted) {
    return { route: 'Dashboard', setupMode: 'None', editingProfileId: null, isSetupComplete: true, currentStepIndex: 0, profileDraftName: '' };
  }

  return { route: 'FirstRun', setupMode: 'FirstRun', editingProfileId: null, isSetupComplete: false, currentStepIndex: 0, profileDraftName: '' };
}

export function assertSetupStateInvariants(route: BootstrapRoute, setupMode: SetupMode, editingProfileId: string | null): void {
  if (setupMode === 'FirstRun' && editingProfileId !== null) throw new Error('FirstRun cannot have an editing profile.');
  if (setupMode === 'EditProfile' && (route === 'FirstRun' || editingProfileId === null)) throw new Error('EditProfile requires a profile setup route and editing profile.');
  if (setupMode === 'CreateProfile' && (route === 'FirstRun' || editingProfileId !== null)) throw new Error('CreateProfile requires a profile setup route and no editing profile.');
}

export function resolveInitialRoute(isHydrated: boolean, isFirstRunCompleted: boolean): InitialRoute {
  if (!isHydrated) return 'Loading';
  return isFirstRunCompleted ? 'Dashboard' : 'FirstRun';
}

export function resolveDashboardSetupMode(action: 'create' | 'edit-profile' | 'edit-global'): SetupMode {
  return action === 'create' ? 'CreateProfile' : action === 'edit-global' ? 'EditGlobal' : 'EditProfile';
}

export function resolveSaveCompletion(mode: SetupMode, currentCompletion: boolean): boolean {
  return mode === 'FirstRun' ? true : currentCompletion;
}

export function resolvePersistedCompletion(mode: SetupMode, persistedCompletion: boolean, requestedCompletion = persistedCompletion): boolean {
  return mode === 'FirstRun' ? requestedCompletion : persistedCompletion;
}

export function resolvePostSaveSetupComplete(mode: SetupMode, persistedCompletion: boolean): boolean {
  return mode === 'FirstRun' ? persistedCompletion : true;
}

export function resolveWorkingProfileName(mode: SetupMode, profileDraftName: string, dashboardSelectedProfileName: string): string {
  return mode === 'FirstRun' || mode === 'CreateProfile' || mode === 'EditProfile'
    ? profileDraftName.trim()
    : dashboardSelectedProfileName.trim();
}

export function canContinueWorkingProfile(mode: SetupMode, profileDraftName: string, displaySelectionCount: number): boolean {
  if (mode !== 'FirstRun' && mode !== 'CreateProfile' && mode !== 'EditProfile') return true;
  return profileDraftName.trim().length > 0 && displaySelectionCount > 0;
}