import { useTranslation } from 'react-i18next';
import type { KeyboardEvent, ReactNode } from 'react';
import { AppBehaviourStep } from '../components/AppBehaviourStep';
import { ConsoleBehaviourStep } from '../components/ConsoleBehaviourStep';
import { ControllerStep } from '../components/ControllerStep';
import { DisplaysStep } from '../components/DisplaysStep';
import { FinishStep } from '../components/FinishStep';
import { LauncherStep } from '../components/LauncherStep';
import { WelcomeStep } from '../components/WelcomeStep';
import { WorkBehaviourStep } from '../components/WorkBehaviourStep';
import type { AppSettings, ControllerShortcutSettings, ControllerStatusInfo, DisplayDeviceInfo, DisplayIdentity, DisplayLayoutProfile, DisplayLayoutSnapshot, GamingProfileDisplayTopology } from '../types/bridge';
import type { LauncherType } from '../utils/modes';

type StepId = 'welcome' | 'gaming-display' | 'launcher' | 'console-behaviour' | 'work-behaviour' | 'work-shortcuts' | 'controller' | 'app-behaviour' | 'finish';
type PendingNavigationAction = 'goBack' | 'goNext' | 'returnToDashboard';
type OverwriteProfileAction = 'next' | 'save' | 'save-then-continue';
type PcGamingBehaviourState = Pick<AppSettings['pcGaming'], 'hideDesktopIcons' | 'hideTaskbar' | 'closeVisibleWindows' | 'minimizeRemainingWindows' | 'moveCursorToCorner' | 'hideCursor' | 'startLauncher' | 'bringLauncherToForeground'>;
type WorkBehaviourState = Pick<AppSettings['workMode'], 'closeLauncher' | 'showTaskbars' | 'showDesktopIcons' | 'doNotMinimizeWindows'>;
type HotkeysState = { consoleMode: string; pcGamingMode: string; workMode: string };
type FinishOptions = { startWithWindows: boolean; startMinimized: boolean; startMenuShortcut: boolean; desktopShortcut: boolean };
type ProfileHotkeysMap = Record<string, { enterProfile: string }>;
type ProfileLauncherMap = Record<string, LauncherType>;
type ProfileControllerMap = Record<string, ControllerShortcutSettings | null>;
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
  displayListError: string | null;
  refreshDisplays: () => Promise<void>;
  launcher: LauncherType;
  setLauncher: (launcher: LauncherType) => void;
  language: import('../i18n/language').SupportedLanguage;
  setLanguage: (value: import('../i18n/language').SupportedLanguage) => void;
  pcGamingBehaviour: PcGamingBehaviourState;
  onChangePcGamingBehaviour: (key: keyof PcGamingBehaviourState, value: boolean) => void;
  workBehaviour: WorkBehaviourState;
  setWorkBehaviour: (behaviour: WorkBehaviourState) => void;
  hotkeys: HotkeysState;
  profileHotkeyValidationError: string | null;
  profileControllerValidationError: string | null;
  workModeControllerShortcut: ControllerShortcutSettings | null;
  profileControllerShortcut: ControllerShortcutSettings | null;
  controllerStatus: ControllerStatusInfo;
  captureSuccessButtons: string[] | null;
  controllerNotice: 'missing' | 'disconnected' | null;
  startControllerCapture: (profileId: string) => void;
  cancelControllerCapture: () => void;
  removeWorkControllerShortcut: () => void;
  removeProfileControllerShortcut: () => void;
  removeWorkHotkey: () => void;
  removeProfileHotkey: () => void;
  changeWorkHotkey: (value: string) => void;
  changeProfileHotkey: (value: string) => void;
  finishOptions: FinishOptions;
  setFinishOptions: (options: FinishOptions) => void;
  pcGamingLayout: DisplayLayoutSnapshot | null;
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
  updateProfileHotkey: (value: string) => void;
  updateProfileControllerShortcut: (buttons: string[]) => void;
  updateWorkModeHotkey: (value: string) => void;
  updateWorkModeControllerShortcut: (buttons: string[]) => void;
};

export function ProfileSetupPage({
  activeSteps, resolvedFlowPosition, currentStep, stepIndex, isVeryFirstConfiguration, isNonFirstRunEditingSession, isEditingSection, isCreatingProfileFlow, isSaving, canGoBack, isLastStep, canSelectSidebarStep, footerMessage, hasError, statusLastError, pendingOverwriteProfileAction, showOverwriteProfileDialog, showUnsavedDialog, displays, profiles, selectedProfileName, displayValidationState, displayDraftChange, setDisplayValidationState, isRefreshingDisplays, displayListError, refreshDisplays, launcher, setLauncher, language, setLanguage, pcGamingBehaviour, onChangePcGamingBehaviour, workBehaviour, setWorkBehaviour, hotkeys, profileHotkeyValidationError, profileControllerValidationError, workModeControllerShortcut, profileControllerShortcut, controllerStatus, captureSuccessButtons, controllerNotice, startControllerCapture, cancelControllerCapture, removeWorkControllerShortcut, removeProfileControllerShortcut, removeWorkHotkey, removeProfileHotkey, changeWorkHotkey, changeProfileHotkey, finishOptions, setFinishOptions, pcGamingLayout, profileLaunchers, profileHotkeys, activeProfileName, requestNavigation, saveAndReturnToDashboard, handleOverwriteConfirm, handleUnsavedSave, handleUnsavedDiscard, setShowOverwriteProfileDialog, setPendingOverwriteProfileAction, setPendingNavigationAction, setShowUnsavedDialog, handleSidebarStepSelect, updateProfileLauncher, updateProfileHotkey, updateProfileControllerShortcut, updateWorkModeHotkey, updateWorkModeControllerShortcut
}: ProfileSetupPageProps) {
  const { t } = useTranslation();
  const sidebarSteps = activeSteps.map((step) => ({
    id: step.id,
    title: t(step.titleKey),
    subtitle: t(step.subtitleKey)
  }));

  const renderStep = (): ReactNode => {
    const stepNumber = resolvedFlowPosition + 1;
    if (currentStep.id === 'welcome') return <WelcomeStep stepNumber={stepIndex + 1} language={language} setLanguage={setLanguage} />;
    if (currentStep.id === 'gaming-display') return <DisplaysStep stepNumber={stepNumber} displays={displays} pcGamingProfiles={profiles} selectedProfileName={selectedProfileName} prefillFromSelectedProfile={isEditingSection} onDraftChange={displayDraftChange} validationState={displayValidationState} onValidationStateChange={setDisplayValidationState} isRefreshingDisplays={isRefreshingDisplays} displayListError={displayListError} refreshDisplays={refreshDisplays} />;
    if (currentStep.id === 'launcher') return <LauncherStep stepNumber={stepNumber} pcGamingProfiles={profiles} selectedProfileName={selectedProfileName} launcher={launcher} setLauncher={updateProfileLauncher} />;
    if (currentStep.id === 'console-behaviour') return <ConsoleBehaviourStep stepNumber={stepNumber} pcGamingProfiles={profiles} selectedProfileName={selectedProfileName} pcGamingBehaviour={pcGamingBehaviour} onChangeBehaviourFlag={onChangePcGamingBehaviour} />;
    if (currentStep.id === 'work-behaviour') return <WorkBehaviourStep stepNumber={stepNumber} behaviour={workBehaviour} setBehaviour={setWorkBehaviour} />;
    if (currentStep.id === 'work-shortcuts') return <ControllerStep stepNumber={stepNumber} pcGamingProfiles={[]} selectedProfileName={t('setup.workMode')} isWorkMode hotkeys={hotkeys} hotkeyValidationError={profileHotkeyValidationError} controllerShortcut={workModeControllerShortcut} controllerValidationError={profileControllerValidationError} controllerStatus={controllerStatus} captureSuccessButtons={captureSuccessButtons} controllerNotice={controllerNotice} onStartControllerCapture={() => startControllerCapture('WorkMode')} onCancelControllerCapture={cancelControllerCapture} onRemoveControllerShortcut={removeWorkControllerShortcut} onRemoveHotkey={removeWorkHotkey} onChangeHotkey={(_, value) => changeWorkHotkey(value)} />;
    if (currentStep.id === 'controller') return <ControllerStep stepNumber={stepNumber} pcGamingProfiles={profiles} selectedProfileName={selectedProfileName} hotkeys={hotkeys} hotkeyValidationError={profileHotkeyValidationError} controllerShortcut={profileControllerShortcut} controllerValidationError={profileControllerValidationError} controllerStatus={controllerStatus} captureSuccessButtons={captureSuccessButtons} controllerNotice={controllerNotice} onStartControllerCapture={() => startControllerCapture(selectedProfileName)} onCancelControllerCapture={cancelControllerCapture} onRemoveControllerShortcut={removeProfileControllerShortcut} onRemoveHotkey={removeProfileHotkey} onChangeHotkey={(_, value) => changeProfileHotkey(value)} />;
    if (currentStep.id === 'app-behaviour') return <AppBehaviourStep stepNumber={stepNumber} finishOptions={finishOptions} setFinishOptions={setFinishOptions} language={language} setLanguage={setLanguage} />;
    return <FinishStep stepNumber={stepNumber} launcher={launcher} pcGamingLayout={pcGamingLayout} pcGamingProfileLaunchers={profileLaunchers} pcGamingProfileHotkeys={profileHotkeys} activeProfileName={activeProfileName} />;
  };

  return <main className="app-shell"><section className="setup-window" aria-labelledby="app-title">
    <aside className="brand-panel" aria-label={t('setup.globalSettingsSidebar')}><ol className="step-list">{sidebarSteps.map((step, index) => <li className={`step-item ${index === resolvedFlowPosition ? 'active' : ''} ${index < resolvedFlowPosition ? 'done' : ''} ${canSelectSidebarStep ? 'clickable' : ''}`} key={step.id} role={canSelectSidebarStep ? 'button' : undefined} tabIndex={canSelectSidebarStep ? 0 : undefined} onClick={() => void handleSidebarStepSelect(step.id)} onKeyDown={(event: KeyboardEvent<HTMLLIElement>) => { if (!canSelectSidebarStep || (event.key !== 'Enter' && event.key !== ' ')) return; event.preventDefault(); void handleSidebarStepSelect(step.id); }}><span>{step.title}</span><small>{step.subtitle}</small></li>)}</ol></aside>
    <div className={`setup-main wizard-main ${isVeryFirstConfiguration ? '' : 'hide-step-eyebrows'}`}><section className="setup-page">{renderStep()}</section>
      <footer className="setup-footer">{isNonFirstRunEditingSession ? <><div className="footer-actions-left"><button className="secondary-button" type="button" disabled={isSaving} onClick={() => requestNavigation('returnToDashboard')}>{t('setup.goToDashboard')}</button></div><p className={hasError || statusLastError ? 'error-line' : 'operation-line'}>{footerMessage}</p><div className="footer-actions-right"><button className="primary-button" type="button" disabled={isSaving} onClick={saveAndReturnToDashboard}>{isSaving ? t('setup.saving') : t('setup.saveChanges')}</button></div></> : isEditingSection ? <><div className="footer-actions-left"><button className="secondary-button" type="button" disabled={!canGoBack || isSaving} onClick={() => requestNavigation('goBack')}>{t('setup.back')}</button><button className="secondary-button" type="button" disabled={isSaving} onClick={() => requestNavigation('returnToDashboard')}>{t('setup.goToDashboard')}</button></div><p className={hasError || statusLastError ? 'error-line' : 'operation-line'}>{footerMessage}</p><div className="footer-actions-right"><button className="secondary-button" type="button" disabled={isSaving || isLastStep} onClick={() => requestNavigation('goNext')}>{t('setup.next')}</button><button className="primary-button" type="button" disabled={isSaving} onClick={saveAndReturnToDashboard}>{isSaving ? t('setup.saving') : t('setup.saveChanges')}</button></div></> : <><div className="footer-actions-left"><button className="secondary-button" type="button" disabled={!canGoBack} onClick={() => requestNavigation('goBack')}>{t('setup.back')}</button>{isCreatingProfileFlow ? <button className="secondary-button" type="button" disabled={isSaving} onClick={() => requestNavigation('returnToDashboard')}>{t('setup.goToDashboard')}</button> : null}</div><p className={hasError || statusLastError ? 'error-line' : 'operation-line'}>{footerMessage}</p><div className="footer-actions-right"><button className="primary-button" type="button" disabled={isSaving} onClick={() => requestNavigation('goNext')}>{isSaving ? t('setup.saving') : isLastStep ? t('setup.goToDashboard') : t('setup.next')}</button></div></>}</footer>
      {showOverwriteProfileDialog ? <div className="dialog-backdrop" role="presentation"><div className="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="overwrite-profile-dialog-title"><h3 id="overwrite-profile-dialog-title">{t('setup.overwriteTitle')}</h3><p>{t('setup.overwriteMessage')}</p><div className="dialog-actions"><button className="secondary-button" type="button" onClick={() => { if (pendingOverwriteProfileAction === 'save-then-continue') setPendingNavigationAction(null); setShowOverwriteProfileDialog(false); setPendingOverwriteProfileAction(null); }}>{t('setup.overwriteCancel')}</button><button className="primary-button" type="button" onClick={() => void handleOverwriteConfirm()}>{t('setup.overwriteConfirm')}</button></div></div></div> : null}
      {showUnsavedDialog ? <div className="dialog-backdrop" role="presentation"><div className="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="unsaved-dialog-title"><h3 id="unsaved-dialog-title">{t('setup.unsavedTitle')}</h3><p>{t('setup.unsavedMessage')}</p><div className="dialog-actions"><button className="secondary-button" type="button" onClick={() => setShowUnsavedDialog(false)}>{t('setup.overwriteCancel')}</button><button className="danger-button secondary-button" type="button" onClick={() => void handleUnsavedDiscard()}>{t('setup.unsavedDiscard')}</button><button className="primary-button" type="button" onClick={() => void handleUnsavedSave()}>{t('setup.unsavedSave')}</button></div></div></div> : null}
    </div>
  </section></main>;
}
