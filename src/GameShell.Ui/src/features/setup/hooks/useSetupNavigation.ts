import type { Dispatch, SetStateAction } from 'react';
import type { AppSettings } from '../../../types/bridge';
import type { SettingsPayloadOverrides } from '../../settings/buildSettingsPayload';

export type PendingNavigationAction = 'goBack' | 'goNext' | 'returnToDashboard';
export type OverwriteProfileAction = 'next' | 'save' | 'save-then-continue';
export type SetupStepId = string;

type UseSetupNavigationOptions = {
  isVeryFirstConfiguration: boolean;
  isCreatingProfileFlow: boolean;
  hasAdvancedInCreateProfileFlow: boolean;
  hasUnsavedChanges: boolean;
  isEditingSection: boolean;
  isLastStep: boolean;
  canSelectSidebarStep: boolean;
  currentStepId: SetupStepId;
  stepIndex: number;
  activeStepIndexes: number[];
  resolvedFlowPosition: number;
  isSetupComplete: boolean;
  pendingNavigationAction: PendingNavigationAction | null;
  pendingOverwriteProfileAction: OverwriteProfileAction | null;
  settings: AppSettings | null;
  pendingSettingsOverrideRef: { current: SettingsPayloadOverrides | null };
  saveAndReturnToDashboard: () => Promise<void>;
  saveCurrentSettings: (isFirstRunCompleted: boolean, overrides?: SettingsPayloadOverrides) => Promise<AppSettings>;
  persistAndReturnToDashboard: () => Promise<void>;
  commitGamingDisplayDraft: (allowOverwrite: boolean, action: OverwriteProfileAction) => Promise<boolean>;
  resolveStepIndex: (stepId: SetupStepId) => number;
  applySettingsToState: (settings: AppSettings) => void;
  resetDisplayProfileDraft: () => void;
  setPendingNavigationAction: Dispatch<SetStateAction<PendingNavigationAction | null>>;
  setPendingOverwriteProfileAction: Dispatch<SetStateAction<OverwriteProfileAction | null>>;
  setShowOverwriteProfileDialog: (value: boolean) => void;
  setShowUnsavedDialog: (value: boolean) => void;
  setError: (value: string | null) => void;
  setMessage: (value: string) => void;
  setStepIndex: (value: number) => void;
  setHasAdvancedInCreateProfileFlow: (value: boolean) => void;
  setIsEditingSection: (value: boolean) => void;
  setIsCreatingProfileFlow: (value: boolean) => void;
  setIsSetupComplete: (value: boolean) => void;
  setIsSaving: (value: boolean) => void;
  setHasUnsavedChanges: (value: boolean) => void;
};

export function useSetupNavigation(options: UseSetupNavigationOptions) {
  const {
    isVeryFirstConfiguration, isCreatingProfileFlow, hasAdvancedInCreateProfileFlow, hasUnsavedChanges,
    isEditingSection, isLastStep, canSelectSidebarStep, currentStepId, stepIndex, activeStepIndexes,
    resolvedFlowPosition, isSetupComplete, pendingNavigationAction, pendingOverwriteProfileAction,
    settings, pendingSettingsOverrideRef, saveAndReturnToDashboard, saveCurrentSettings,
    persistAndReturnToDashboard, commitGamingDisplayDraft, resolveStepIndex, applySettingsToState,
    resetDisplayProfileDraft, setPendingNavigationAction, setPendingOverwriteProfileAction,
    setShowOverwriteProfileDialog, setShowUnsavedDialog, setError, setMessage, setStepIndex,
    setHasAdvancedInCreateProfileFlow, setIsEditingSection, setIsCreatingProfileFlow, setIsSetupComplete,
    setIsSaving, setHasUnsavedChanges
  } = options;

  function requestNavigation(action: PendingNavigationAction) {
    if (isVeryFirstConfiguration || action === 'goNext' || (isCreatingProfileFlow && action === 'goBack') || (isCreatingProfileFlow && action === 'returnToDashboard' && !hasAdvancedInCreateProfileFlow) || !hasUnsavedChanges) {
      void executeNavigation(action);
      return;
    }
    setPendingNavigationAction(action);
    setShowUnsavedDialog(true);
  }

  async function handleSidebarStepSelect(stepId: SetupStepId) {
    if (!canSelectSidebarStep) return;
    const nextIndex = resolveStepIndex(stepId);
    if (nextIndex < 0 || nextIndex === stepIndex) return;
    if (currentStepId === 'gaming-display' && !(await commitGamingDisplayDraft(false, 'next'))) return;
    setError(null);
    setStepIndex(nextIndex);
    setMessage('Wybrano sekcję do edycji.');
  }

  async function goNextInternal() {
    setError(null);
    if (currentStepId === 'gaming-display') {
      await continueFromGamingDisplayStep(false);
      return;
    }
    if (isLastStep) {
      await saveAndReturnToDashboard();
      return;
    }
    const nextIndex = activeStepIndexes[resolvedFlowPosition + 1];
    if (typeof nextIndex === 'number') {
      setStepIndex(nextIndex);
      if (isCreatingProfileFlow) setHasAdvancedInCreateProfileFlow(true);
    }
    setMessage(isEditingSection ? 'Możesz przejść dalej lub zapisać zmiany.' : 'Gotowe.');
  }

  async function continueFromGamingDisplayStep(allowOverwrite: boolean) {
    if (!(await commitGamingDisplayDraft(allowOverwrite, 'next'))) return;
    const nextIndex = activeStepIndexes[resolvedFlowPosition + 1];
    if (typeof nextIndex === 'number') {
      setStepIndex(nextIndex);
      if (isCreatingProfileFlow) setHasAdvancedInCreateProfileFlow(true);
    }
    setMessage(isEditingSection ? 'Możesz przejrzeć kolejne sekcje albo zapisać zmiany.' : 'Gotowe.');
  }

  async function executeNavigation(action: PendingNavigationAction) {
    if (action === 'goBack') {
      setError(null);
      const previousIndex = activeStepIndexes[resolvedFlowPosition - 1];
      if (typeof previousIndex === 'number') setStepIndex(previousIndex);
      return;
    }
    if (action === 'goNext') {
      await goNextInternal();
      return;
    }
    setError(null);
    setIsEditingSection(false);
    setIsCreatingProfileFlow(false);
    setHasAdvancedInCreateProfileFlow(false);
    setIsSetupComplete(true);
    resetDisplayProfileDraft();
    setShowOverwriteProfileDialog(false);
    setPendingOverwriteProfileAction(null);
    pendingSettingsOverrideRef.current = null;
    setMessage('Gotowe.');
  }

  async function handleOverwriteProfileDialogConfirm() {
    setShowOverwriteProfileDialog(false);
    const action = pendingOverwriteProfileAction;
    setPendingOverwriteProfileAction(null);
    if (!(await commitGamingDisplayDraft(true, action ?? 'next'))) return;
    if (action === 'save') {
      await new Promise((resolve) => setTimeout(resolve, 0));
      await persistAndReturnToDashboard();
      return;
    }
    if (action === 'save-then-continue') {
      try {
        setIsSaving(true);
        await new Promise((resolve) => setTimeout(resolve, 0));
        await saveCurrentSettings(isSetupComplete, pendingSettingsOverrideRef.current ?? undefined);
        pendingSettingsOverrideRef.current = null;
        setHasUnsavedChanges(false);
        if (pendingNavigationAction) {
          const nextAction = pendingNavigationAction;
          setPendingNavigationAction(null);
          await executeNavigation(nextAction);
        }
      } catch (reason: unknown) {
        setError(reason instanceof Error ? reason.message : 'Nie udało się zapisać zmian.');
      } finally {
        setIsSaving(false);
      }
      return;
    }
    const nextIndex = activeStepIndexes[resolvedFlowPosition + 1];
    if (typeof nextIndex === 'number') setStepIndex(nextIndex);
    setMessage(isEditingSection ? 'Możesz przejrzeć kolejne sekcje albo zapisać zmiany.' : 'Gotowe.');
  }

  async function handleUnsavedDialogSave() {
    if (!pendingNavigationAction) {
      setShowUnsavedDialog(false);
      return;
    }
    setShowUnsavedDialog(false);
    if (pendingNavigationAction === 'goNext' && isLastStep) {
      await saveAndReturnToDashboard();
      setPendingNavigationAction(null);
      return;
    }
    let shouldReset = true;
    try {
      setIsSaving(true);
      if (currentStepId === 'gaming-display') {
        if (!(await commitGamingDisplayDraft(false, 'save-then-continue'))) {
          shouldReset = false;
          return;
        }
        await new Promise((resolve) => setTimeout(resolve, 0));
      }
      await saveCurrentSettings(isSetupComplete, pendingSettingsOverrideRef.current ?? undefined);
      pendingSettingsOverrideRef.current = null;
      setHasUnsavedChanges(false);
      await executeNavigation(pendingNavigationAction);
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Nie udało się zapisać zmian.');
    } finally {
      setIsSaving(false);
      if (shouldReset) setPendingNavigationAction(null);
    }
  }

  async function handleUnsavedDialogDiscard() {
    const action = pendingNavigationAction;
    setShowUnsavedDialog(false);
    setPendingNavigationAction(null);
    if (settings) applySettingsToState(settings);
    else {
      setHasUnsavedChanges(false);
      pendingSettingsOverrideRef.current = null;
    }
    if (action) await executeNavigation(action);
  }

  return { requestNavigation, handleSidebarStepSelect, executeNavigation, handleOverwriteProfileDialogConfirm, handleUnsavedDialogSave, handleUnsavedDialogDiscard };
}
