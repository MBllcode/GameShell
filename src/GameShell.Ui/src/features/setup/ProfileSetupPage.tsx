import { useTranslation } from 'react-i18next';
import type { KeyboardEvent, ReactNode } from 'react';
import { AppBehaviourStep } from '../settings/components/AppBehaviourStep';
import { ProfileBehaviourStep } from '../profiles/components/ProfileBehaviourStep';
import { ControllerStep } from '../shortcuts/components/ControllerStep';
import { DisplaysStep } from '../displays/components/DisplaysStep';
import { FinishStep } from './components/FinishStep';
import { LauncherStep } from '../profiles/components/LauncherStep';
import { WelcomeStep } from './components/WelcomeStep';
import { WorkBehaviourStep } from '../settings/components/WorkBehaviourStep';
import { UiIcon } from '../../shared/ui/UiPrimitives';
import { ViewportContent } from '../../shared/ui/ViewportContent';
import type { AppSettings, ControllerShortcutSettings, ControllerStatusInfo, DisplayDeviceInfo, DisplayLayoutProfile, DisplayLayoutSnapshot, GamingProfileDisplayTopology } from '../../bridge/contracts';
import type { LauncherType } from '../profiles/profileCommands';

type StepId = 'welcome' | 'gaming-display' | 'launcher' | 'profile-behaviour' | 'work-display' | 'work-behaviour' | 'work-shortcuts' | 'controller' | 'app-behaviour' | 'finish';
type PendingNavigationAction = 'goBack' | 'goNext' | 'returnToDashboard' | 'skipProfile';
type OverwriteProfileAction = 'next' | 'save' | 'save-then-continue';
type GamingBehaviourState = Pick<AppSettings['gaming'], 'hideDesktopIcons' | 'hideTaskbar' | 'closeVisibleWindows' | 'minimizeRemainingWindows' | 'moveCursorToCorner' | 'hideCursor' | 'startLauncher' | 'bringLauncherToForeground'>;
type WorkBehaviourState = Pick<AppSettings['workMode'], 'closeLauncher' | 'showTaskbars' | 'showDesktopIcons' | 'doNotMinimizeWindows'>;
type HotkeysState = { consoleMode: string; gamingMode: string; workMode: string };
type FinishOptions = { startWithWindows: boolean; startMinimized: boolean; startMenuShortcut: boolean; desktopShortcut: boolean; startupBehavior: 'AlwaysWorkMode' | 'RestoreLastActiveMode' | 'AlwaysProfile'; startupProfileId: string | null };
type ProfileHotkeysMap = Record<string, { enterProfile: string }>;
type ProfileLauncherMap = Record<string, LauncherType>;
type DisplayValidationState = { showNameError: boolean; showSelectionError: boolean };

export type ProfileSetupPageProps = {
  activeSteps: { id: StepId; titleKey: string; subtitleKey: string }[];
  resolvedFlowPosition: number;
  currentStep: { id: StepId };
  stepIndex: number;
  isVeryFirstConfiguration: boolean;
  isNonFirstRunEditingSession: boolean;
  isEditingSection: boolean;
  isCreatingProfileFlow: boolean;
  isSaving: boolean;
  canGoBack: boolean;
  isLastStep: boolean;
  canSelectSidebarStep: boolean;
  footerMessage: string;
  hasError: boolean;
  statusLastError: string | null;
  pendingOverwriteProfileAction: OverwriteProfileAction | null;
  showOverwriteProfileDialog: boolean;
  showUnsavedDialog: boolean;
  displays: DisplayDeviceInfo[];
  profiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  displayValidationState: DisplayValidationState;
  displayDraftChange: (profileName: string, topology: GamingProfileDisplayTopology) => void;
  setDisplayValidationState: (state: DisplayValidationState) => void;
  isRefreshingDisplays: boolean;
  isDisplaySettling: boolean;
  displayListError: string | null;
  refreshDisplays: () => Promise<void>;
  workModeLayout: DisplayLayoutSnapshot | null;
  workModeSavedLayout: DisplayLayoutSnapshot | null;
  onWorkModeDraftChange: (topology: GamingProfileDisplayTopology) => void;
  launcher: LauncherType;
  language: import('../../i18n/language').SupportedLanguage;
  setLanguage: (value: import('../../i18n/language').SupportedLanguage) => void;
  gamingBehaviour: GamingBehaviourState;
  onChangeGamingBehaviour: (key: keyof GamingBehaviourState, value: boolean) => void;
  workBehaviour: WorkBehaviourState;
  setWorkBehaviour: (behaviour: WorkBehaviourState) => void;
  hotkeys: HotkeysState;
  profileHotkeyValidationError: string | null;
  profileControllerValidationError: string | null;
  onHotkeyValidationErrorChange: (value: string | null) => void;
  workModeControllerShortcut: ControllerShortcutSettings | null;
  profileControllerShortcut: ControllerShortcutSettings | null;
  controllerStatus: ControllerStatusInfo;
  captureSuccessButtons: string[] | null;
  rejectedControllerButtons: string[] | null;
  onControllerValidationErrorChange: (value: string | null) => void;
  controllerNotice: 'missing' | 'disconnected' | null;
  startControllerCapture: (profileId: string) => void;
  cancelControllerCapture: () => void;
  removeWorkControllerShortcut: () => void;
  removeProfileControllerShortcut: () => void;
  removeWorkHotkey: () => void;
  removeProfileHotkey: () => void;
  changeWorkHotkey: (value: string) => boolean;
  changeProfileHotkey: (value: string) => boolean;
  finishOptions: FinishOptions;
  setFinishOptions: (options: FinishOptions) => void;
  gamingLayout: DisplayLayoutSnapshot | null;
  profileLaunchers: ProfileLauncherMap;
  profileHotkeys: ProfileHotkeysMap;
  activeProfileName: string | null;
  requestNavigation: (action: PendingNavigationAction) => void;
  saveAndReturnToDashboard: () => Promise<void>;
  handleOverwriteConfirm: () => Promise<void>;
  handleUnsavedSave: () => Promise<void>;
  handleUnsavedDiscard: () => Promise<void>;
  setShowOverwriteProfileDialog: (show: boolean) => void;
  setPendingOverwriteProfileAction: (action: OverwriteProfileAction | null) => void;
  setPendingNavigationAction: (action: PendingNavigationAction | null) => void;
  setShowUnsavedDialog: (show: boolean) => void;
  handleSidebarStepSelect: (stepId: StepId) => Promise<void>;
  updateProfileLauncher: (launcher: LauncherType) => void;
};

export function ProfileSetupPage({
  activeSteps, resolvedFlowPosition, currentStep, stepIndex, isVeryFirstConfiguration, isNonFirstRunEditingSession, isEditingSection, isCreatingProfileFlow, isSaving, canGoBack, isLastStep, canSelectSidebarStep, footerMessage, hasError, statusLastError, pendingOverwriteProfileAction, showOverwriteProfileDialog, showUnsavedDialog, displays, profiles, selectedProfileName, displayValidationState, displayDraftChange, setDisplayValidationState, isRefreshingDisplays, isDisplaySettling, displayListError, refreshDisplays, launcher, language, setLanguage, gamingBehaviour, onChangeGamingBehaviour, workBehaviour, setWorkBehaviour, hotkeys, profileHotkeyValidationError, profileControllerValidationError, onHotkeyValidationErrorChange, workModeControllerShortcut, profileControllerShortcut, controllerStatus, captureSuccessButtons, rejectedControllerButtons, onControllerValidationErrorChange, controllerNotice, startControllerCapture, cancelControllerCapture, removeWorkControllerShortcut, removeProfileControllerShortcut, removeWorkHotkey, removeProfileHotkey, changeWorkHotkey, changeProfileHotkey, finishOptions, setFinishOptions, gamingLayout, profileLaunchers, profileHotkeys, activeProfileName, workModeLayout, workModeSavedLayout, onWorkModeDraftChange, requestNavigation, saveAndReturnToDashboard, handleOverwriteConfirm, handleUnsavedSave, handleUnsavedDiscard, setShowOverwriteProfileDialog, setPendingOverwriteProfileAction, setPendingNavigationAction, setShowUnsavedDialog, handleSidebarStepSelect, updateProfileLauncher}: ProfileSetupPageProps) {
  const { t } = useTranslation();
  const sidebarSteps = activeSteps.map((step) => ({
    id: step.id,
    title: t(step.titleKey),
    subtitle: t(step.subtitleKey)
  }));
  const hasShortcutValidationError = Boolean(profileHotkeyValidationError || profileControllerValidationError);

  const renderStep = (): ReactNode => {
    const stepNumber = resolvedFlowPosition + 1;
    if (currentStep.id === 'welcome') return <WelcomeStep stepNumber={stepIndex + 1} language={language} setLanguage={setLanguage} />;
    if (currentStep.id === 'gaming-display') return <DisplaysStep stepNumber={stepNumber} displays={displays} gamingProfiles={profiles} selectedProfileName={selectedProfileName} prefillFromSelectedProfile={isEditingSection} onDraftChange={displayDraftChange} validationState={displayValidationState} onValidationStateChange={setDisplayValidationState} isRefreshingDisplays={isRefreshingDisplays} isDisplaySettling={isDisplaySettling} displayListError={displayListError} refreshDisplays={refreshDisplays} />;
    if (currentStep.id === 'work-display') return <DisplaysStep stepNumber={stepNumber} displays={displays} gamingProfiles={[]} selectedProfileName="" prefillFromSelectedProfile={isEditingSection} onDraftChange={() => undefined} validationState={displayValidationState} onValidationStateChange={setDisplayValidationState} isRefreshingDisplays={isRefreshingDisplays} isDisplaySettling={isDisplaySettling} displayListError={displayListError} refreshDisplays={refreshDisplays} displayMode="work" workModeLayout={workModeLayout} workModeSavedLayout={workModeSavedLayout} onWorkModeDraftChange={onWorkModeDraftChange} />;
    if (currentStep.id === 'launcher') return <LauncherStep stepNumber={stepNumber} selectedProfileName={selectedProfileName} launcher={launcher} setLauncher={updateProfileLauncher} />;
    if (currentStep.id === 'profile-behaviour') return <ProfileBehaviourStep stepNumber={stepNumber} selectedProfileName={selectedProfileName} gamingBehaviour={gamingBehaviour} onChangeBehaviourFlag={onChangeGamingBehaviour} />;
    if (currentStep.id === 'work-behaviour') return <WorkBehaviourStep stepNumber={stepNumber} behaviour={workBehaviour} setBehaviour={setWorkBehaviour} />;
    if (currentStep.id === 'work-shortcuts') return <ControllerStep stepNumber={stepNumber} selectedProfileName={t('setup.workMode')} isWorkMode hotkeys={hotkeys} hotkeyValidationError={profileHotkeyValidationError} onHotkeyValidationErrorChange={onHotkeyValidationErrorChange} controllerShortcut={workModeControllerShortcut} controllerValidationError={profileControllerValidationError} controllerStatus={controllerStatus} captureSuccessButtons={captureSuccessButtons} rejectedControllerButtons={rejectedControllerButtons} onControllerValidationErrorChange={onControllerValidationErrorChange} controllerNotice={controllerNotice} onStartControllerCapture={() => startControllerCapture('WorkMode')} onCancelControllerCapture={cancelControllerCapture} onRemoveControllerShortcut={removeWorkControllerShortcut} onRemoveHotkey={removeWorkHotkey} onChangeHotkey={(_, value) => changeWorkHotkey(value)} />;
    if (currentStep.id === 'controller') return <ControllerStep stepNumber={stepNumber} selectedProfileName={selectedProfileName} hotkeys={hotkeys} hotkeyValidationError={profileHotkeyValidationError} onHotkeyValidationErrorChange={onHotkeyValidationErrorChange} controllerShortcut={profileControllerShortcut} controllerValidationError={profileControllerValidationError} controllerStatus={controllerStatus} captureSuccessButtons={captureSuccessButtons} rejectedControllerButtons={rejectedControllerButtons} onControllerValidationErrorChange={onControllerValidationErrorChange} controllerNotice={controllerNotice} onStartControllerCapture={() => startControllerCapture(selectedProfileName)} onCancelControllerCapture={cancelControllerCapture} onRemoveControllerShortcut={removeProfileControllerShortcut} onRemoveHotkey={removeProfileHotkey} onChangeHotkey={(_, value) => changeProfileHotkey(value)} />;
    if (currentStep.id === 'app-behaviour') return <AppBehaviourStep stepNumber={stepNumber} finishOptions={finishOptions} setFinishOptions={setFinishOptions} language={language} setLanguage={setLanguage} showLanguageSelector={!isVeryFirstConfiguration} profiles={profiles} />;
    return <FinishStep stepNumber={stepNumber} launcher={launcher} gamingLayout={gamingLayout} gamingProfileLaunchers={profileLaunchers} gamingProfileHotkeys={profileHotkeys} activeProfileName={activeProfileName} />;
  };

  return <main className="app-shell"><section className="setup-window" aria-labelledby="app-title">
    <aside className="brand-panel" aria-label={t('setup.globalSettingsSidebar')}><ol className="step-list">{sidebarSteps.map((step, index) => <li className={`step-item ${index === resolvedFlowPosition ? 'active' : ''} ${index < resolvedFlowPosition ? 'done' : ''} ${canSelectSidebarStep ? 'clickable' : ''}`} key={step.id} role={canSelectSidebarStep ? 'button' : undefined} tabIndex={canSelectSidebarStep ? 0 : undefined} onClick={() => void handleSidebarStepSelect(step.id)} onKeyDown={(event: KeyboardEvent<HTMLLIElement>) => { if (!canSelectSidebarStep || (event.key !== 'Enter' && event.key !== ' ')) return; event.preventDefault(); void handleSidebarStepSelect(step.id); }}><span>{step.title}</span><small>{step.subtitle}</small></li>)}</ol></aside>
    <div className={`setup-main wizard-main ${isVeryFirstConfiguration ? '' : 'hide-step-eyebrows'}`}><section className="setup-page"><ViewportContent>{renderStep()}</ViewportContent></section>
      <footer className="setup-footer">
        {isNonFirstRunEditingSession ? (
          <>
            <div className="footer-actions-left"><button className="secondary-button" type="button" disabled={isSaving} onClick={() => requestNavigation('returnToDashboard')}><UiIcon name="dashboard" size={15} />{t('setup.goToDashboard')}</button></div>
            <p className={hasError || statusLastError ? 'error-line' : 'operation-line'}>{footerMessage}</p>
            <div className="footer-actions-right"><button className="primary-button" type="button" disabled={isSaving || hasShortcutValidationError} onClick={saveAndReturnToDashboard}><UiIcon name="save" size={15} />{isSaving ? t('setup.saving') : t('setup.saveChanges')}</button></div>
          </>
        ) : isEditingSection ? (
          <>
            <div className="footer-actions-left"><button className="secondary-button" type="button" disabled={!canGoBack || isSaving} onClick={() => requestNavigation('goBack')}><UiIcon name="layout" size={15} />{t('setup.back')}</button><button className="secondary-button" type="button" disabled={isSaving} onClick={() => requestNavigation('returnToDashboard')}><UiIcon name="dashboard" size={15} />{t('setup.goToDashboard')}</button></div>
            <p className={hasError || statusLastError ? 'error-line' : 'operation-line'}>{footerMessage}</p>
            <div className="footer-actions-right"><button className="secondary-button" type="button" disabled={isSaving || isLastStep} onClick={() => requestNavigation('goNext')}><UiIcon name="play" size={15} />{t('setup.next')}</button><button className="primary-button" type="button" disabled={isSaving || hasShortcutValidationError} onClick={saveAndReturnToDashboard}><UiIcon name="save" size={15} />{isSaving ? t('setup.saving') : t('setup.saveChanges')}</button></div>
          </>
        ) : (
          <>
            <div className="footer-actions-left"><button className="secondary-button" type="button" disabled={!canGoBack} onClick={() => requestNavigation('goBack')}><UiIcon name="layout" size={15} />{t('setup.back')}</button>{isCreatingProfileFlow ? <button className="secondary-button" type="button" disabled={isSaving} onClick={() => requestNavigation('returnToDashboard')}><UiIcon name="dashboard" size={15} />{t('setup.goToDashboard')}</button> : null}</div>
            <p className={hasError || statusLastError ? 'error-line' : 'operation-line'}>{footerMessage}</p>
            <div className="footer-actions-right">{isVeryFirstConfiguration && currentStep.id === 'gaming-display' && profiles.length === 0 ? <button className="secondary-button" type="button" disabled={isSaving} onClick={() => requestNavigation('skipProfile')}>{t('setup.skipProfile')}</button> : null}<button className="primary-button" type="button" disabled={isSaving} onClick={() => requestNavigation('goNext')}><UiIcon name={isLastStep && isCreatingProfileFlow ? 'save' : isLastStep ? 'dashboard' : 'play'} size={15} />{isSaving ? t('setup.saving') : isLastStep && isCreatingProfileFlow ? t('setup.saveChanges') : isLastStep ? t('setup.goToDashboard') : t('setup.next')}</button></div>
          </>
        )}
      </footer>
      {showOverwriteProfileDialog ? <div className="dialog-backdrop" role="presentation"><div className="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="overwrite-profile-dialog-title"><h3 id="overwrite-profile-dialog-title">{t('setup.overwriteTitle')}</h3><p>{t('setup.overwriteMessage')}</p><div className="dialog-actions"><button className="secondary-button" type="button" onClick={() => { if (pendingOverwriteProfileAction === 'save-then-continue') setPendingNavigationAction(null); setShowOverwriteProfileDialog(false); setPendingOverwriteProfileAction(null); }}>{t('setup.overwriteCancel')}</button><button className="primary-button" type="button" onClick={() => void handleOverwriteConfirm()}>{t('setup.overwriteConfirm')}</button></div></div></div> : null}
      {showUnsavedDialog ? <div className="dialog-backdrop" role="presentation"><div className="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="unsaved-dialog-title"><h3 id="unsaved-dialog-title">{t('setup.unsavedTitle')}</h3><p>{t('setup.unsavedMessage')}</p><div className="dialog-actions"><button className="secondary-button" type="button" onClick={() => setShowUnsavedDialog(false)}>{t('setup.overwriteCancel')}</button><button className="danger-button secondary-button" type="button" onClick={() => void handleUnsavedDiscard()}>{t('setup.unsavedDiscard')}</button><button className="primary-button" type="button" disabled={hasShortcutValidationError} onClick={() => void handleUnsavedSave()}>{t('setup.unsavedSave')}</button></div></div></div> : null}
    </div>
  </section></main>;
}
