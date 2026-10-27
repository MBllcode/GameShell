import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { GamingProfileDisplayTopology } from '../../../bridge/contracts';
import type { SettingsPayloadOverrides } from '../../settings/buildSettingsPayload';
import type { DisplayProfileDraft, DisplayValidationState, OverwriteProfileAction } from '../../profiles/hooks/useDisplayProfileWorkflow';
import type { PendingNavigationAction } from './useSetupNavigation';
import type { SetupMode } from '../setupFlow';
import type { BootstrapRoute } from '../setupRouting';

const KNOWN_MESSAGE_RETRANSLATIONS: Array<{ pattern: RegExp; key: string }> = [
  { pattern: /^You are editing the selected section\.$/, key: 'dashboard.editSection' },
  { pattern: /^Create a new profile and walk through the profile steps\.$/, key: 'dashboard.createProfileHint' },
  { pattern: /^Selected section for editing\.$/, key: 'setup.sectionSelected' },
  { pattern: /^Done\.$/, key: 'setup.ready' },
  { pattern: /^You can continue later or save changes\.$/, key: 'setup.continueLater' },
  { pattern: /^Switching to .+\.\.\.$/, key: 'dashboard.switchingTo' }
];

const DEFAULT_TRANSIENT_MESSAGE_DURATION_MS = 2500;

export function useSetupWorkflowState() {
  const { t, i18n } = useTranslation();
  const [stepIndex, setStepIndex] = useState(0);
  const [message, setMessageState] = useState('');
  const messageRef = useRef(message);
  const transientMessageTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const messageVersionRef = useRef(0);
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
  const [displayProfileDraft, setDisplayProfileDraft] = useState<DisplayProfileDraft>({ profileName: '', topology: { displays: [] } });
  const [displayValidationState, setDisplayValidationState] = useState<DisplayValidationState>({ showNameError: false, showSelectionError: false });
  const displayProfileDraftRef = useRef<DisplayProfileDraft>({ profileName: '', topology: { displays: [] } });
  const pendingSettingsOverrideRef = useRef<SettingsPayloadOverrides | null>(null);

  const setMessage = useCallback((nextMessage: string | ((current: string) => string)) => {
    if (transientMessageTimerRef.current) {
      clearTimeout(transientMessageTimerRef.current);
      transientMessageTimerRef.current = null;
    }

    messageVersionRef.current += 1;
    const resolvedMessage = typeof nextMessage === 'function' ? nextMessage(messageRef.current) : nextMessage;
    messageRef.current = resolvedMessage;
    setMessageState(resolvedMessage);
  }, []);

  const setTransientMessage = useCallback((nextMessage: string, durationMs = DEFAULT_TRANSIENT_MESSAGE_DURATION_MS) => {
    const nextVersion = messageVersionRef.current + 1;
    setMessage(nextMessage);

    transientMessageTimerRef.current = setTimeout(() => {
      if (messageVersionRef.current === nextVersion) setMessage('');
      transientMessageTimerRef.current = null;
    }, durationMs);
  }, [setMessage]);

  useEffect(() => () => {
    if (transientMessageTimerRef.current) clearTimeout(transientMessageTimerRef.current);
  }, []);

  useEffect(() => {
    const translatedEntry = KNOWN_MESSAGE_RETRANSLATIONS.find(({ pattern }) => pattern.test(messageRef.current));
    if (!translatedEntry) return;

    if (translatedEntry.key === 'dashboard.switchingTo') {
      const modeMatch = messageRef.current.match(/^Switching to (.+)\.\.\.$/);
      setMessageState(modeMatch ? t(translatedEntry.key, { mode: modeMatch[1] }) : t(translatedEntry.key));
      return;
    }

    setMessageState(t(translatedEntry.key));
  }, [i18n.language, t]);

  const syncDisplayProfileDraft = useCallback((nextDraft: DisplayProfileDraft) => {
    displayProfileDraftRef.current = nextDraft;
    setDisplayProfileDraft(nextDraft);
  }, []);
  const markUnsavedChanges = useCallback(() => setHasUnsavedChanges(true), []);
  const handleDisplayDraftChange = useCallback((profileName: string, topology: GamingProfileDisplayTopology) => {
    syncDisplayProfileDraft({ profileName, topology });
  }, [syncDisplayProfileDraft]);
  const handleDisplayValidationStateChange = useCallback((state: DisplayValidationState) => setDisplayValidationState(state), []);

  return {
    stepIndex, setStepIndex, message, setMessage, setTransientMessage, error, setError, isSaving, setIsSaving, isInitialLoading, setIsInitialLoading,
    setupMode, setSetupMode, isHydrated, setIsHydrated, bootstrapRoute, setBootstrapRoute, editingProfileId, setEditingProfileId, isVeryFirstConfiguration: setupMode === 'FirstRun', isEditingSection: setupMode === 'EditProfile' || setupMode === 'EditGlobal',
    isCreatingProfileFlow: setupMode === 'CreateProfile', isSetupComplete, setIsSetupComplete,
    hasAdvancedInCreateProfileFlow, setHasAdvancedInCreateProfileFlow,
    hasUnsavedChanges, setHasUnsavedChanges, pendingNavigationAction, setPendingNavigationAction, showUnsavedDialog, setShowUnsavedDialog,
    showOverwriteProfileDialog, setShowOverwriteProfileDialog, pendingOverwriteProfileAction, setPendingOverwriteProfileAction,
    displayProfileDraft, displayValidationState, setDisplayValidationState, displayProfileDraftRef, pendingSettingsOverrideRef, syncDisplayProfileDraft,
    markUnsavedChanges, handleDisplayDraftChange, handleDisplayValidationStateChange
  };
}
