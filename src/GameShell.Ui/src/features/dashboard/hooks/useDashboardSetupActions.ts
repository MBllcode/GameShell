import type { Dispatch, SetStateAction } from 'react';
import type { DisplayLayoutProfile, DisplayIdentity } from '../../../types/bridge';
import { extractDisplayIdentities } from '../../../utils/displays';

type StepId =
  | 'welcome'
  | 'gaming-display'
  | 'launcher'
  | 'console-behaviour'
  | 'work-behaviour'
  | 'work-shortcuts'
  | 'controller'
  | 'app-behaviour'
  | 'finish';

type Options = {
  steps: readonly { id: StepId }[];
  profiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  setStepIndex: Dispatch<SetStateAction<number>>;
  setIsEditingSection: Dispatch<SetStateAction<boolean>>;
  setIsCreatingProfileFlow: Dispatch<SetStateAction<boolean>>;
  setHasAdvancedInCreateProfileFlow: Dispatch<SetStateAction<boolean>>;
  setIsSetupComplete: Dispatch<SetStateAction<boolean>>;
  syncDisplayProfileDraft: (draft: { profileName: string; identities: DisplayIdentity[] }) => void;
  setProfileHotkeyValidationError: Dispatch<SetStateAction<string | null>>;
  setProfileControllerValidationError: Dispatch<SetStateAction<string | null>>;
  setHotkeys: Dispatch<SetStateAction<{ consoleMode: string; pcGamingMode: string; workMode: string }>>;
  setShowOverwriteProfileDialog: Dispatch<SetStateAction<boolean>>;
  setMessage: Dispatch<SetStateAction<string>>;
};

export function useDashboardSetupActions(options: Options) {
  const createProfile = () => {
    const targetIndex = options.steps.findIndex((step) => step.id === 'gaming-display');
    options.setIsEditingSection(false);
    options.setIsCreatingProfileFlow(true);
    options.setHasAdvancedInCreateProfileFlow(false);
    options.setIsSetupComplete(false);
    options.syncDisplayProfileDraft({ profileName: '', identities: [] });
    options.setProfileHotkeyValidationError(null);
    options.setProfileControllerValidationError(null);
    options.setHotkeys((current) => ({ ...current, pcGamingMode: '' }));
    options.setShowOverwriteProfileDialog(false);
    options.setStepIndex(targetIndex >= 0 ? targetIndex : 0);
    options.setMessage('Dodaj nowy profil i przejdź przez kroki profilu.');
  };

  const editSettings = (stepId: StepId) => {
    const targetIndex = options.steps.findIndex((step) => step.id === stepId);
    options.setIsEditingSection(true);
    options.setIsCreatingProfileFlow(false);
    options.setHasAdvancedInCreateProfileFlow(false);
    options.setIsSetupComplete(false);
    if (stepId === 'gaming-display') {
      const selectedProfile = options.profiles.find((profile) => profile.name === options.selectedProfileName) ?? null;
      if (selectedProfile) {
        options.syncDisplayProfileDraft({
          profileName: selectedProfile.name,
          identities: extractDisplayIdentities(selectedProfile.layout)
        });
      }
    }
    options.setStepIndex(targetIndex >= 0 ? targetIndex : 0);
    options.setMessage('Edytujesz wybraną sekcję.');
  };

  return { createProfile, editSettings };
}
