import { useCallback, useRef, useState } from 'react';
import type { DisplayIdentity } from '../../../types/bridge';
import type { SettingsPayloadOverrides } from '../../settings/buildSettingsPayload';
import type { DisplayProfileDraft, DisplayValidationState, OverwriteProfileAction } from '../../profiles/hooks/useDisplayProfileWorkflow';
import type { PendingNavigationAction } from './useSetupNavigation';

export function useSetupWorkflowState() {
  const [stepIndex, setStepIndex] = useState(0);
  const [message, setMessage] = useState('Gotowe.');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [isInitialLoading, setIsInitialLoading] = useState(true);
  const [isVeryFirstConfiguration, setIsVeryFirstConfiguration] = useState(true);
  const [isSetupComplete, setIsSetupComplete] = useState(false);
  const [isEditingSection, setIsEditingSection] = useState(false);
  const [isCreatingProfileFlow, setIsCreatingProfileFlow] = useState(false);
  const [hasAdvancedInCreateProfileFlow, setHasAdvancedInCreateProfileFlow] = useState(false);
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  const [pendingNavigationAction, setPendingNavigationAction] = useState<PendingNavigationAction | null>(null);
  const [showUnsavedDialog, setShowUnsavedDialog] = useState(false);
  const [showOverwriteProfileDialog, setShowOverwriteProfileDialog] = useState(false);
  const [pendingOverwriteProfileAction, setPendingOverwriteProfileAction] = useState<OverwriteProfileAction | null>(null);
  const [displayProfileDraft, setDisplayProfileDraft] = useState<DisplayProfileDraft>({ profileName: '', identities: [] });
  const [displayValidationState, setDisplayValidationState] = useState<DisplayValidationState>({ showNameError: false, showSelectionError: false });
  const displayProfileDraftRef = useRef<DisplayProfileDraft>({ profileName: '', identities: [] });
  const pendingSettingsOverrideRef = useRef<SettingsPayloadOverrides | null>(null);

  const syncDisplayProfileDraft = useCallback((nextDraft: DisplayProfileDraft) => {
    displayProfileDraftRef.current = nextDraft;
    setDisplayProfileDraft(nextDraft);
  }, []);
  const markUnsavedChanges = useCallback(() => setHasUnsavedChanges(true), []);
  const handleDisplayDraftChange = useCallback((profileName: string, identities: DisplayIdentity[]) => {
    syncDisplayProfileDraft({ profileName, identities });
  }, [syncDisplayProfileDraft]);
  const handleDisplayValidationStateChange = useCallback((state: DisplayValidationState) => setDisplayValidationState(state), []);

  return {
    stepIndex, setStepIndex, message, setMessage, error, setError, isSaving, setIsSaving, isInitialLoading, setIsInitialLoading,
    isVeryFirstConfiguration, setIsVeryFirstConfiguration, isSetupComplete, setIsSetupComplete, isEditingSection, setIsEditingSection,
    isCreatingProfileFlow, setIsCreatingProfileFlow, hasAdvancedInCreateProfileFlow, setHasAdvancedInCreateProfileFlow,
    hasUnsavedChanges, setHasUnsavedChanges, pendingNavigationAction, setPendingNavigationAction, showUnsavedDialog, setShowUnsavedDialog,
    showOverwriteProfileDialog, setShowOverwriteProfileDialog, pendingOverwriteProfileAction, setPendingOverwriteProfileAction,
    displayProfileDraft, displayValidationState, setDisplayValidationState, displayProfileDraftRef, pendingSettingsOverrideRef, syncDisplayProfileDraft,
    markUnsavedChanges, handleDisplayDraftChange, handleDisplayValidationStateChange
  };
}
