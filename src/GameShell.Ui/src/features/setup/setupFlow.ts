export type StepId =
  | 'welcome'
  | 'gaming-display'
  | 'launcher'
  | 'console-behaviour'
  | 'work-behaviour'
  | 'work-shortcuts'
  | 'controller'
  | 'app-behaviour'
  | 'finish';

export type SetupMode = 'None' | 'FirstRun' | 'CreateProfile' | 'EditProfile' | 'EditGlobal';

export type SetupStep = { id: StepId; title: string; subtitle: string };

export const FIRST_RUN_STEPS: SetupStep[] = [
  { id: 'welcome', title: 'Start', subtitle: 'Najważniejsze ustawienia' },
  { id: 'gaming-display', title: 'Ekrany', subtitle: 'Ekrany używane przez profil' },
  { id: 'launcher', title: 'Launcher profilu', subtitle: 'Aplikacja uruchamiana z profilem' },
  { id: 'console-behaviour', title: 'Zachowanie profilu', subtitle: 'Pulpit po uruchomieniu profilu' },
  { id: 'controller', title: 'Skróty profilu', subtitle: 'Skróty uruchamiające profil' },
  { id: 'work-behaviour', title: 'Zachowanie Work Mode', subtitle: 'Pulpit po powrocie do pracy' },
  { id: 'work-shortcuts', title: 'Skróty Work Mode', subtitle: 'Skróty powrotu do pracy' },
  { id: 'app-behaviour', title: 'Zachowanie aplikacji', subtitle: 'Uruchamianie i skróty systemowe' },
  { id: 'finish', title: 'Zakończenie', subtitle: 'Sprawdź ustawienia profilu' },
];

export const PROFILE_STEPS: SetupStep[] = FIRST_RUN_STEPS.filter((step) => ['gaming-display', 'launcher', 'console-behaviour', 'controller'].includes(step.id));
export const GLOBAL_STEPS: SetupStep[] = FIRST_RUN_STEPS.filter((step) => ['work-behaviour', 'work-shortcuts', 'app-behaviour'].includes(step.id));
export const steps = FIRST_RUN_STEPS;

export function getSetupSteps(mode: SetupMode) {
  return mode === 'FirstRun' ? FIRST_RUN_STEPS : mode === 'EditGlobal' ? GLOBAL_STEPS : PROFILE_STEPS;
}

export function resolveSetupSteps(mode: SetupMode, currentStepId: StepId) {
  const activeSteps = getSetupSteps(mode);
  const activeStepIndexes = activeSteps.map((step) => FIRST_RUN_STEPS.findIndex((entry) => entry.id === step.id));
  return { activeSteps, activeStepIndexes };
}
