import type { OverwriteProfileAction } from '../../profiles/hooks/useDisplayProfileWorkflow';

type Options = {
  isCreatingProfileFlow: boolean;
  currentStepId: string;
  commitGamingDisplayDraft: (allowOverwrite: boolean, action: OverwriteProfileAction) => Promise<boolean>;
  persistAndReturnToDashboard: () => Promise<void>;
};

export function useSetupSaveWorkflow({ isCreatingProfileFlow, currentStepId, commitGamingDisplayDraft, persistAndReturnToDashboard }: Options) {
  return async function saveAndReturnToDashboard() {
    if (isCreatingProfileFlow || currentStepId === 'gaming-display') {
      const committed = await commitGamingDisplayDraft(false, 'save');
      if (!committed) return;
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    await persistAndReturnToDashboard();
  };
}
