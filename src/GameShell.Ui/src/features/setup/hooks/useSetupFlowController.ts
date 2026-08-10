import { useEffect, useRef } from 'react';
import type { Dispatch, SetStateAction } from 'react';
import type { DisplayDeviceInfo, DisplayIdentity, DisplayLayoutProfile } from '../../../types/bridge';
import type { ControllerCaptureApi } from '../../controller/hooks/useControllerCapture';
import type { PcGamingProfileBehaviourMap, PcGamingProfileHotkeysMap, PcGamingProfileLauncherMap } from '../../profiles/utils';
import type { LauncherType } from '../../../utils/modes';
import type { PcGamingBehaviourState, HotkeySettingsState } from '../../settings/hooks/useSettingsFormState';
import { syncActiveDisplaySelections } from '../../displays/syncActiveDisplaySelections';
import { defaultProfileHotkeys } from '../../settings/hooks/useSettingsFormState';
import { resolveSetupSteps, steps, type SetupMode, type StepId } from '../setupFlow';
import { normalizeHotkeyForDisplay } from '../../../utils/hotkeys';

type Options = {
  stepIndex: number;
  setupMode: SetupMode;
  activePcGamingProfileName: string | null;
  selectedProfileName: string;
  profiles: DisplayLayoutProfile[];
  profileBehaviours: PcGamingProfileBehaviourMap;
  profileLaunchers: PcGamingProfileLauncherMap;
  profileHotkeys: PcGamingProfileHotkeysMap;
  pcGamingBehaviour: PcGamingBehaviourState;
  launcher: LauncherType;
  setSelectedConsoleDisplays: Dispatch<SetStateAction<DisplayIdentity[]>>;
  setSelectedConsoleDisplay: Dispatch<SetStateAction<DisplayIdentity | null>>;
  setSelectedProfileName: Dispatch<SetStateAction<string>>;
  setPcGamingBehaviour: Dispatch<SetStateAction<PcGamingBehaviourState>>;
  setLauncher: Dispatch<SetStateAction<LauncherType>>;
  setHotkeys: Dispatch<SetStateAction<HotkeySettingsState>>;
  displays: DisplayDeviceInfo[];
  controllerCapture: ControllerCaptureApi;
};

export function useSetupFlowController(options: Options) {
  const indexedStep = steps[options.stepIndex] ?? steps[0];
  const { activeSteps, activeStepIndexes } = resolveSetupSteps(options.setupMode, indexedStep.id);
  const fallbackStepIndex = activeStepIndexes[0] ?? 0;
  const currentFlowPosition = activeStepIndexes.indexOf(options.stepIndex);
  const resolvedFlowPosition = currentFlowPosition >= 0 ? currentFlowPosition : 0;
  const resolvedStepIndex = currentFlowPosition >= 0 ? options.stepIndex : fallbackStepIndex;
  const currentStep = steps[resolvedStepIndex] ?? steps[0];

  useEffect(() => {
    const selections = syncActiveDisplaySelections(options.displays);
    options.setSelectedConsoleDisplays(selections.consoleDisplays);
    options.setSelectedConsoleDisplay(selections.primaryDisplay);
  }, [options.displays]);

  useEffect(() => {
    if (options.setupMode === 'FirstRun') {
      options.setSelectedProfileName('');
      return;
    }
    const hasSelectedProfile = options.selectedProfileName.length > 0
      && options.profiles.some((profile) => profile.name === options.selectedProfileName);

    const fallbackProfileName = hasSelectedProfile
      ? options.selectedProfileName
      : options.activePcGamingProfileName && options.profiles.some((profile) => profile.name === options.activePcGamingProfileName)
        ? options.activePcGamingProfileName
        : options.profiles[0]?.name ?? '';

    if (!hasSelectedProfile) {
      options.setSelectedProfileName(fallbackProfileName);
    }

    if (fallbackProfileName) {
      options.setPcGamingBehaviour(options.profileBehaviours[fallbackProfileName] ?? options.pcGamingBehaviour);
      options.setLauncher(options.profileLaunchers[fallbackProfileName] ?? options.launcher);
      options.setHotkeys((current) => ({
        ...current,
        pcGamingMode: normalizeHotkeyForDisplay(options.profileHotkeys[fallbackProfileName]?.enterProfile ?? defaultProfileHotkeys.enterProfile)
      }));
    }
  }, [options.activePcGamingProfileName, options.launcher, options.pcGamingBehaviour, options.profileBehaviours, options.profileHotkeys, options.profileLaunchers, options.profiles, options.selectedProfileName]);

  const previousStepRef = useRef<StepId | null>(null);
  useEffect(() => {
    const previousStep = previousStepRef.current;
    previousStepRef.current = currentStep.id;
    if ((previousStep === 'controller' || previousStep === 'work-shortcuts')
      && currentStep.id !== 'controller'
      && currentStep.id !== 'work-shortcuts'
      && options.controllerCapture.sessionRef.current) {
      void options.controllerCapture.cancelActiveCapture();
    }
  }, [currentStep.id]);

  return {
    activeSteps,
    activeStepIndexes,
    currentStep,
    resolvedFlowPosition,
    isNonFirstRunEditingSession: options.setupMode === 'EditProfile' || options.setupMode === 'EditGlobal',
    isLastStep: resolvedFlowPosition === activeStepIndexes.length - 1,
    canGoBack: resolvedFlowPosition > 0
  };
}
