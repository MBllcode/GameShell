export type StepId =
  | 'welcome'
  | 'gaming-display'
  | 'work-display'
  | 'launcher'
  | 'profile-behaviour'
  | 'work-behaviour'
  | 'work-shortcuts'
  | 'controller'
  | 'app-behaviour'
  | 'finish';

export type SetupMode = 'None' | 'FirstRun' | 'CreateProfile' | 'EditProfile' | 'EditGlobal';

export type SetupStep = { id: StepId; titleKey: string; subtitleKey: string };

export const FIRST_RUN_STEPS: SetupStep[] = [
  { id: 'welcome', titleKey: 'setup.start', subtitleKey: 'setup.firstRun' },
  { id: 'work-display', titleKey: 'setup.workModeDisplays', subtitleKey: 'setup.defaultDisplayLayout' },
  { id: 'work-behaviour', titleKey: 'globalSection.workBehaviour', subtitleKey: 'setup.environmentRestore' },
  { id: 'work-shortcuts', titleKey: 'setup.workModeShortcuts', subtitleKey: 'setup.returnToWork' },
  { id: 'gaming-display', titleKey: 'setup.displays', subtitleKey: 'setup.profileSetup' },
  { id: 'launcher', titleKey: 'setup.launcherProfile', subtitleKey: 'setup.launcherActivation' },
  { id: 'profile-behaviour', titleKey: 'setup.profileBehaviour', subtitleKey: 'setup.desktopSetup' },
  { id: 'controller', titleKey: 'setup.profileShortcuts', subtitleKey: 'setup.inputActivation' },
  { id: 'app-behaviour', titleKey: 'setup.appBehaviour', subtitleKey: 'setup.startupIntegration' },
  { id: 'finish', titleKey: 'setup.summary', subtitleKey: 'setup.reviewAndSave' },
];

export const PROFILE_STEPS: SetupStep[] = FIRST_RUN_STEPS.filter((step) => ['gaming-display', 'launcher', 'profile-behaviour', 'controller'].includes(step.id));
export const GLOBAL_STEPS: SetupStep[] = FIRST_RUN_STEPS.filter((step) => ['work-display', 'work-behaviour', 'work-shortcuts', 'app-behaviour'].includes(step.id));
export const steps = FIRST_RUN_STEPS;

export function getSetupSteps(mode: SetupMode) {
  return mode === 'FirstRun' ? FIRST_RUN_STEPS : mode === 'EditGlobal' ? GLOBAL_STEPS : PROFILE_STEPS;
}

export function resolveSetupSteps(mode: SetupMode) {
  const activeSteps = getSetupSteps(mode);
  const activeStepIndexes = activeSteps.map((step) => FIRST_RUN_STEPS.findIndex((entry) => entry.id === step.id));
  return { activeSteps, activeStepIndexes };
}
