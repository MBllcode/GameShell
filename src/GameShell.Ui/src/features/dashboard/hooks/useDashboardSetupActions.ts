import { useTranslation } from 'react-i18next';
import type { Dispatch, SetStateAction } from 'react';
import type { DisplayLayoutProfile, GamingProfileDisplayTopology } from '../../../types/bridge';
import { getProfileTopology } from '../../../utils/displays';
import type { SetupMode } from '../../setup/setupFlow';
import { assertSetupStateInvariants, type BootstrapRoute } from '../../setup/setupRouting';
import { defaultGamingBehaviour, type GamingBehaviourState } from '../../settings/hooks/useSettingsFormState';

type StepId =
  | 'welcome'
  | 'gaming-display'
  | 'work-display'
  | 'launcher'
  | 'profile-behaviour'
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
  setHasUnsavedChanges: Dispatch<SetStateAction<boolean>>;
  setIsSetupComplete: Dispatch<SetStateAction<boolean>>;
  syncDisplayProfileDraft: (draft: { profileName: string; topology: GamingProfileDisplayTopology }) => void;
  setProfileHotkeyValidationError: Dispatch<SetStateAction<string | null>>;
  setProfileControllerValidationError: Dispatch<SetStateAction<string | null>>;
  setHotkeys: Dispatch<SetStateAction<{ consoleMode: string; gamingMode: string; workMode: string }>>;
  setGamingBehaviour: Dispatch<SetStateAction<GamingBehaviourState>>;
  setShowOverwriteProfileDialog: Dispatch<SetStateAction<boolean>>;
  setMessage: Dispatch<SetStateAction<string>>;
};

export function useDashboardSetupActions(options: Options) {
  const { t } = useTranslation();

  const createProfile = () => {
    const targetIndex = options.steps.findIndex((step) => step.id === 'gaming-display');
    assertSetupStateInvariants('ProfileSetup', 'CreateProfile', null);
    options.setSetupMode('CreateProfile');
    options.setBootstrapRoute('ProfileSetup');
    options.setEditingProfileId(null);
    options.setHasAdvancedInCreateProfileFlow(false);
    options.setHasUnsavedChanges(false);
    options.setIsSetupComplete(false);
    options.setGamingBehaviour({ ...defaultGamingBehaviour });
    options.syncDisplayProfileDraft({ profileName: '', topology: { displays: [] } });
    options.setProfileHotkeyValidationError(null);
    options.setProfileControllerValidationError(null);
    options.setHotkeys((current) => ({ ...current, gamingMode: '' }));
    options.setShowOverwriteProfileDialog(false);
    options.setStepIndex(targetIndex >= 0 ? targetIndex : 0);
    options.setMessage(t('dashboard.createProfileHint'));
  };

  const editSettings = (stepId: StepId) => {
    const targetIndex = options.steps.findIndex((step) => step.id === stepId);
    const selectedProfile = options.profiles.find((profile) => profile.name === options.selectedProfileName) ?? null;
    const nextMode = ['work-display', 'work-behaviour', 'work-shortcuts', 'app-behaviour'].includes(stepId) ? 'EditGlobal' : 'EditProfile';
    assertSetupStateInvariants('ProfileSetup', nextMode, nextMode === 'EditProfile' ? selectedProfile?.name ?? null : null);
    options.setSetupMode(nextMode);
    options.setBootstrapRoute('ProfileSetup');
    options.setEditingProfileId(nextMode === 'EditProfile' ? selectedProfile?.name ?? null : null);
    options.setHasAdvancedInCreateProfileFlow(false);
    options.setHasUnsavedChanges(false);
    options.setIsSetupComplete(false);
    if (selectedProfile) {
      options.syncDisplayProfileDraft({
        profileName: selectedProfile.name,
        topology: getProfileTopology(selectedProfile)
      });
    } else {
      options.syncDisplayProfileDraft({ profileName: '', topology: { displays: [] } });
    }
    options.setStepIndex(targetIndex >= 0 ? targetIndex : 0);
    options.setMessage(t('dashboard.editSection')); 
  };

  return { createProfile, editSettings };
}
