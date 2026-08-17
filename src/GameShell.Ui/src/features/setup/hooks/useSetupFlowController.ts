import { useEffect, useRef } from 'react';
import type { Dispatch, SetStateAction } from 'react';
import type { DisplayDeviceInfo, DisplayIdentity, DisplayLayoutProfile } from '../../../types/bridge';
import type { ControllerCaptureApi } from '../../controller/hooks/useControllerCapture';
import type { GamingProfileBehaviourMap, GamingProfileHotkeysMap, GamingProfileLauncherMap } from '../../profiles/utils';
import type { LauncherType } from '../../../utils/modes';
import type { GamingBehaviourState, HotkeySettingsState } from '../../settings/hooks/useSettingsFormState';
import { syncActiveDisplaySelections } from '../../displays/syncActiveDisplaySelections';
import { defaultProfileHotkeys } from '../../settings/hooks/useSettingsFormState';
import { resolveSetupSteps, steps, type SetupMode, type StepId } from '../setupFlow';
import { normalizeHotkeyForDisplay } from '../../../utils/hotkeys';

type Options = {
  stepIndex: number;
  setupMode: SetupMode;
  activeGamingProfileName: string | null;
  selectedProfileName: string;
  profiles: DisplayLayoutProfile[];
  profileBehaviours: GamingProfileBehaviourMap;
  profileLaunchers: GamingProfileLauncherMap;
  profileHotkeys: GamingProfileHotkeysMap;
  gamingBehaviour: GamingBehaviourState;
  launcher: LauncherType;
  setSelectedConsoleDisplays: Dispatch<SetStateAction<DisplayIdentity[]>>;
  setSelectedConsoleDisplay: Dispatch<SetStateAction<DisplayIdentity | null>>;
  setSelectedProfileName: Dispatch<SetStateAction<string>>;
  setGamingBehaviour: Dispatch<SetStateAction<GamingBehaviourState>>;
  setLauncher: Dispatch<SetStateAction<LauncherType>>;
  setHotkeys: Dispatch<SetStateAction<HotkeySettingsState>>;
  displays: DisplayDeviceInfo[];
  selectedConsoleDisplays: DisplayIdentity[];
  selectedConsoleDisplay: DisplayIdentity | null;
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
    // Discovery refreshes are observational. Console display defaults are
    // inferred only during first-run; editing screens must never replace a
    // saved selection with the current Windows observation.
    if (options.setupMode !== 'FirstRun'
      || options.selectedConsoleDisplays.length > 0
      || options.selectedConsoleDisplay !== null) {
      return;
    }

    const selections = syncActiveDisplaySelections(options.displays);
    options.setSelectedConsoleDisplays(selections.consoleDisplays);
    options.setSelectedConsoleDisplay(selections.primaryDisplay);
  }, [options.displays, options.selectedConsoleDisplay, options.selectedConsoleDisplays, options.setSelectedConsoleDisplay, options.setSelectedConsoleDisplays]);

  useEffect(() => {
    if (options.setupMode === 'FirstRun') {
      options.setSelectedProfileName('');
      return;
    }
    const hasSelectedProfile = options.selectedProfileName.length > 0
      && options.profiles.some((profile) => profile.name === options.selectedProfileName);

    const fallbackProfileName = hasSelectedProfile
      ? options.selectedProfileName
      : options.activeGamingProfileName && options.profiles.some((profile) => profile.name === options.activeGamingProfileName)
        ? options.activeGamingProfileName
        : options.profiles[0]?.name ?? '';

    if (!hasSelectedProfile) {
      options.setSelectedProfileName(fallbackProfileName);
    }

    if (fallbackProfileName) {
      options.setGamingBehaviour(options.profileBehaviours[fallbackProfileName] ?? options.gamingBehaviour);
      options.setLauncher(options.profileLaunchers[fallbackProfileName] ?? options.launcher);
      options.setHotkeys((current) => ({
        ...current,
        gamingMode: normalizeHotkeyForDisplay(options.profileHotkeys[fallbackProfileName]?.enterProfile ?? defaultProfileHotkeys.enterProfile)
      }));
    }
  }, [options.activeGamingProfileName, options.launcher, options.gamingBehaviour, options.profileBehaviours, options.profileHotkeys, options.profileLaunchers, options.profiles, options.selectedProfileName]);

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
