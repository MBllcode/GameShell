import type { ProfileSetupPageProps } from '../../../pages/ProfileSetupPage';

type Options = {
  flow: Pick<ProfileSetupPageProps, 'activeSteps' | 'resolvedFlowPosition' | 'currentStep' | 'stepIndex' | 'isVeryFirstConfiguration' | 'isNonFirstRunEditingSession' | 'isEditingSection' | 'isCreatingProfileFlow' | 'isSaving' | 'canGoBack' | 'isLastStep' | 'canSelectSidebarStep' | 'footerMessage' | 'hasError' | 'statusLastError' | 'pendingOverwriteProfileAction' | 'showOverwriteProfileDialog' | 'showUnsavedDialog'>;
  displays: Pick<ProfileSetupPageProps, 'displays' | 'profiles' | 'selectedProfileName' | 'displayValidationState' | 'displayDraftChange' | 'setDisplayValidationState' | 'isRefreshingDisplays' | 'displayListError' | 'refreshDisplays' | 'pcGamingLayout'>;
  profile: Pick<ProfileSetupPageProps, 'launcher' | 'setLauncher' | 'pcGamingBehaviour' | 'onChangePcGamingBehaviour' | 'workBehaviour' | 'setWorkBehaviour' | 'hotkeys' | 'profileHotkeyValidationError' | 'profileControllerValidationError' | 'profileLaunchers' | 'profileHotkeys' | 'activeProfileName'>;
  controller: Pick<ProfileSetupPageProps, 'workModeControllerShortcut' | 'profileControllerShortcut' | 'controllerStatus' | 'captureSuccessButtons' | 'controllerNotice'>;
  actions: Pick<ProfileSetupPageProps, 'startControllerCapture' | 'cancelControllerCapture' | 'removeWorkControllerShortcut' | 'removeProfileControllerShortcut' | 'removeWorkHotkey' | 'removeProfileHotkey' | 'changeWorkHotkey' | 'changeProfileHotkey' | 'setFinishOptions' | 'requestNavigation' | 'saveAndReturnToDashboard' | 'handleOverwriteConfirm' | 'handleUnsavedSave' | 'handleUnsavedDiscard' | 'setShowOverwriteProfileDialog' | 'setPendingOverwriteProfileAction' | 'setPendingNavigationAction' | 'setShowUnsavedDialog' | 'handleSidebarStepSelect' | 'updateProfileLauncher' | 'updateProfileHotkey' | 'updateProfileControllerShortcut' | 'updateWorkModeHotkey' | 'updateWorkModeControllerShortcut'> & Pick<ProfileSetupPageProps, 'finishOptions'>;
};

export function useProfileSetupPageModel({ flow, displays, profile, controller, actions }: Options): ProfileSetupPageProps {
  return { ...flow, ...displays, ...profile, ...controller, ...actions };
}
