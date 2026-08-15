import type { ProfileSetupPageProps } from '../../../pages/ProfileSetupPage';

type Options = {
  flow: Pick<ProfileSetupPageProps, 'activeSteps' | 'resolvedFlowPosition' | 'currentStep' | 'stepIndex' | 'isVeryFirstConfiguration' | 'isNonFirstRunEditingSession' | 'isEditingSection' | 'isCreatingProfileFlow' | 'isSaving' | 'canGoBack' | 'isLastStep' | 'canSelectSidebarStep' | 'footerMessage' | 'hasError' | 'statusLastError' | 'pendingOverwriteProfileAction' | 'showOverwriteProfileDialog' | 'showUnsavedDialog'>;
  displays: Pick<ProfileSetupPageProps, 'displays' | 'profiles' | 'selectedProfileName' | 'displayValidationState' | 'displayDraftChange' | 'setDisplayValidationState' | 'isRefreshingDisplays' | 'displayListError' | 'refreshDisplays' | 'gamingLayout' | 'workModeLayout' | 'workModeSavedLayout' | 'onWorkModeDraftChange'>;
  profile: Pick<ProfileSetupPageProps, 'launcher' | 'setLauncher' | 'language' | 'setLanguage' | 'gamingBehaviour' | 'onChangeGamingBehaviour' | 'workBehaviour' | 'setWorkBehaviour' | 'hotkeys' | 'profileHotkeyValidationError' | 'profileControllerValidationError' | 'profileLaunchers' | 'profileHotkeys' | 'activeProfileName'>;
  controller: Pick<ProfileSetupPageProps, 'workModeControllerShortcut' | 'profileControllerShortcut' | 'controllerStatus' | 'captureSuccessButtons' | 'rejectedControllerButtons' | 'onHotkeyValidationErrorChange' | 'onControllerValidationErrorChange' | 'controllerNotice'>;
  actions: Pick<ProfileSetupPageProps, 'startControllerCapture' | 'cancelControllerCapture' | 'removeWorkControllerShortcut' | 'removeProfileControllerShortcut' | 'removeWorkHotkey' | 'removeProfileHotkey' | 'changeWorkHotkey' | 'changeProfileHotkey' | 'setFinishOptions' | 'requestNavigation' | 'saveAndReturnToDashboard' | 'handleOverwriteConfirm' | 'handleUnsavedSave' | 'handleUnsavedDiscard' | 'setShowOverwriteProfileDialog' | 'setPendingOverwriteProfileAction' | 'setPendingNavigationAction' | 'setShowUnsavedDialog' | 'handleSidebarStepSelect' | 'updateProfileLauncher' | 'updateProfileHotkey' | 'updateProfileControllerShortcut' | 'updateWorkModeHotkey' | 'updateWorkModeControllerShortcut'> & Pick<ProfileSetupPageProps, 'finishOptions'>;
};

export function useProfileSetupPageModel({ flow, displays, profile, controller, actions }: Options): ProfileSetupPageProps {
  return { ...flow, ...displays, ...profile, ...controller, ...actions };
}
