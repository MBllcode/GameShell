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

export type SetupStep = { id: StepId; title: string; subtitle: string };

export const steps: SetupStep[] = [
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

export const profileSettingsStepIds: StepId[] = ['gaming-display', 'launcher', 'console-behaviour', 'controller'];
export const globalSettingsStepIds: StepId[] = ['work-behaviour', 'work-shortcuts', 'app-behaviour'];

export function isGlobalSettingsStep(stepId: StepId) {
  return globalSettingsStepIds.includes(stepId);
}

export function resolveSetupSteps(isVeryFirstConfiguration: boolean, isCreatingProfileFlow: boolean, isEditingSection: boolean, currentStepId: StepId) {
  const activeStepIds = isVeryFirstConfiguration
    ? steps.map((step) => step.id)
    : isCreatingProfileFlow
      ? profileSettingsStepIds
      : isEditingSection
        ? isGlobalSettingsStep(currentStepId) ? globalSettingsStepIds : profileSettingsStepIds
        : steps.map((step) => step.id);
  const activeSteps = activeStepIds.map((id) => steps.find((step) => step.id === id) ?? null).filter((step): step is SetupStep => step !== null);
  const activeStepIndexes = activeSteps.map((step) => steps.findIndex((entry) => entry.id === step.id));
  return { activeSteps, activeStepIndexes };
}
