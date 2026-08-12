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
  { id: 'welcome', title: 'Start', subtitle: 'Pierwsza konfiguracja' },
  { id: 'gaming-display', title: 'Ekrany', subtitle: 'Ustawienie profilu' },
  { id: 'launcher', title: 'Launcher profilu', subtitle: 'Aktywacja aplikacji' },
  { id: 'console-behaviour', title: 'Zachowanie profilu', subtitle: 'Przygotowanie pulpitu' },
  { id: 'controller', title: 'Skróty profilu', subtitle: 'Wejście i aktywacja' },
  { id: 'work-behaviour', title: 'Work Mode', subtitle: 'Przywrócenie środowiska' },
  { id: 'work-shortcuts', title: 'Skróty Work Mode', subtitle: 'Powrót do pracy' },
  { id: 'app-behaviour', title: 'Zachowanie aplikacji', subtitle: 'Uruchamianie i integracja' },
  { id: 'finish', title: 'Podsumowanie', subtitle: 'Sprawdź i zapisz' },
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
