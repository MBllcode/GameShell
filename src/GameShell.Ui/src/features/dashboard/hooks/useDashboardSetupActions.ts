import type { Dispatch, SetStateAction } from 'react';
import type { DisplayLayoutProfile, DisplayIdentity } from '../../../types/bridge';
import { extractDisplayIdentities } from '../../../utils/displays';
import type { SetupMode } from '../../setup/setupFlow';
import { assertSetupStateInvariants, type BootstrapRoute } from '../../setup/setupRouting';
import { defaultPcGamingBehaviour, type PcGamingBehaviourState } from '../../settings/hooks/useSettingsFormState';

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
  setSetupMode: Dispatch<SetStateAction<SetupMode>>;
  setBootstrapRoute: Dispatch<SetStateAction<BootstrapRoute>>;
  setEditingProfileId: Dispatch<SetStateAction<string | null>>;
  setHasAdvancedInCreateProfileFlow: Dispatch<SetStateAction<boolean>>;
  setIsSetupComplete: Dispatch<SetStateAction<boolean>>;
  syncDisplayProfileDraft: (draft: { profileName: string; identities: DisplayIdentity[] }) => void;
  setProfileHotkeyValidationError: Dispatch<SetStateAction<string | null>>;
  setProfileControllerValidationError: Dispatch<SetStateAction<string | null>>;
  setHotkeys: Dispatch<SetStateAction<{ consoleMode: string; pcGamingMode: string; workMode: string }>>;
  setPcGamingBehaviour: Dispatch<SetStateAction<PcGamingBehaviourState>>;
  setShowOverwriteProfileDialog: Dispatch<SetStateAction<boolean>>;
  setMessage: Dispatch<SetStateAction<string>>;
};

export function useDashboardSetupActions(options: Options) {
  const createProfile = () => {
    const targetIndex = options.steps.findIndex((step) => step.id === 'gaming-display');
    assertSetupStateInvariants('ProfileSetup', 'CreateProfile', null);
    options.setSetupMode('CreateProfile');
    options.setBootstrapRoute('ProfileSetup');
    options.setEditingProfileId(null);
    options.setHasAdvancedInCreateProfileFlow(false);
    options.setIsSetupComplete(false);
    options.setPcGamingBehaviour({ ...defaultPcGamingBehaviour });
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
    const selectedProfile = options.profiles.find((profile) => profile.name === options.selectedProfileName) ?? null;
    const nextMode = ['work-behaviour', 'work-shortcuts', 'app-behaviour'].includes(stepId) ? 'EditGlobal' : 'EditProfile';
    assertSetupStateInvariants('ProfileSetup', nextMode, nextMode === 'EditProfile' ? selectedProfile?.name ?? null : null);
    options.setSetupMode(nextMode);
    options.setBootstrapRoute('ProfileSetup');
    options.setEditingProfileId(nextMode === 'EditProfile' ? selectedProfile?.name ?? null : null);
    options.setHasAdvancedInCreateProfileFlow(false);
    options.setIsSetupComplete(false);
    if (selectedProfile) {
      options.syncDisplayProfileDraft({
        profileName: selectedProfile.name,
        identities: extractDisplayIdentities(selectedProfile.layout)
      });
    } else {
      options.syncDisplayProfileDraft({ profileName: '', identities: [] });
    }
    options.setStepIndex(targetIndex >= 0 ? targetIndex : 0);
    options.setMessage('Edytujesz wybraną sekcję.');
  };

  return { createProfile, editSettings };
}
