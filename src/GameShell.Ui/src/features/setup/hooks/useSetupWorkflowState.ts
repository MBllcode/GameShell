import { useCallback, useRef, useState } from 'react';
import type { DisplayIdentity } from '../../../types/bridge';
import type { SettingsPayloadOverrides } from '../../settings/buildSettingsPayload';
import type { DisplayProfileDraft, DisplayValidationState, OverwriteProfileAction } from '../../profiles/hooks/useDisplayProfileWorkflow';
import type { PendingNavigationAction } from './useSetupNavigation';
import type { SetupMode } from '../setupFlow';
import type { BootstrapRoute } from '../setupRouting';

export function useSetupWorkflowState() {
  const [stepIndex, setStepIndex] = useState(0);
  const [message, setMessage] = useState('Gotowe.');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [isInitialLoading, setIsInitialLoading] = useState(true);
  const [setupMode, setSetupMode] = useState<SetupMode>('None');
  const [isHydrated, setIsHydrated] = useState(false);
  const [bootstrapRoute, setBootstrapRoute] = useState<BootstrapRoute>('Loading');
  const [editingProfileId, setEditingProfileId] = useState<string | null>(null);
  const [isSetupComplete, setIsSetupComplete] = useState(false);
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
    setupMode, setSetupMode, isHydrated, setIsHydrated, bootstrapRoute, setBootstrapRoute, editingProfileId, setEditingProfileId, isVeryFirstConfiguration: setupMode === 'FirstRun', isEditingSection: setupMode === 'EditProfile' || setupMode === 'EditGlobal',
    isCreatingProfileFlow: setupMode === 'CreateProfile', isSetupComplete, setIsSetupComplete,
    hasAdvancedInCreateProfileFlow, setHasAdvancedInCreateProfileFlow,
    hasUnsavedChanges, setHasUnsavedChanges, pendingNavigationAction, setPendingNavigationAction, showUnsavedDialog, setShowUnsavedDialog,
    showOverwriteProfileDialog, setShowOverwriteProfileDialog, pendingOverwriteProfileAction, setPendingOverwriteProfileAction,
    displayProfileDraft, displayValidationState, setDisplayValidationState, displayProfileDraftRef, pendingSettingsOverrideRef, syncDisplayProfileDraft,
    markUnsavedChanges, handleDisplayDraftChange, handleDisplayValidationStateChange
  };
}
