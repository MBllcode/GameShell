import type { OverwriteProfileAction } from '../../profiles/hooks/useDisplayProfileWorkflow';
import type { SetupMode } from '../setupFlow';

type Options = {
  setupMode: SetupMode;
  currentStepId: string;
  hasShortcutValidationError: boolean;
  commitGamingDisplayDraft: (allowOverwrite: boolean, action: OverwriteProfileAction) => Promise<boolean>;
  persistAndReturnToDashboard: () => Promise<void>;
};

export function useSetupSaveWorkflow({ setupMode, currentStepId, hasShortcutValidationError, commitGamingDisplayDraft, persistAndReturnToDashboard }: Options) {
  return async function saveAndReturnToDashboard() {
    if (hasShortcutValidationError) return;
    if (setupMode === 'CreateProfile' || currentStepId === 'gaming-display') {
      const committed = await commitGamingDisplayDraft(false, 'save');
      if (!committed) return;
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    await persistAndReturnToDashboard();
  };
}
