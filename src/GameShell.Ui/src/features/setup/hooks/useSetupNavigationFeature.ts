import { useSetupNavigation } from './useSetupNavigation';
import { useSetupSaveWorkflow } from './useSetupSaveWorkflow';

type SaveOptions = Parameters<typeof useSetupSaveWorkflow>[0];
type NavigationOptions = Parameters<typeof useSetupNavigation>[0];

type Options = SaveOptions & Omit<NavigationOptions, 'saveAndReturnToDashboard'>;

export function useSetupNavigationFeature(options: Options) {
  const saveAndReturnToDashboard = useSetupSaveWorkflow({
    setupMode: options.setupMode,
    currentStepId: options.currentStepId,
    hasShortcutValidationError: options.hasShortcutValidationError,
    commitGamingDisplayDraft: options.commitGamingDisplayDraft,
    persistAndReturnToDashboard: options.persistAndReturnToDashboard
  });

  const navigation = useSetupNavigation({
    ...options,
    saveAndReturnToDashboard
  });

  return { saveAndReturnToDashboard, navigation };
}
