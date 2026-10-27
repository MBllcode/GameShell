import { useSetupNavigation } from './useSetupNavigation';

type Options = Omit<Parameters<typeof useSetupNavigation>[0], 'saveAndReturnToDashboard'>;

export function useSetupNavigationFeature(options: Options) {
  async function saveAndReturnToDashboard() {
    if (options.hasShortcutValidationError) return;
    if (options.setupMode === 'CreateProfile' || options.currentStepId === 'gaming-display') {
      if (!await options.commitGamingDisplayDraft(false, 'save')) return;
      // The pending payload override also covers this commit before React publishes state.
      await new Promise(resolve => setTimeout(resolve, 0));
    }
    await options.persistAndReturnToDashboard();
  }

  const navigation = useSetupNavigation({ ...options, saveAndReturnToDashboard });
  return { saveAndReturnToDashboard, navigation };
}
