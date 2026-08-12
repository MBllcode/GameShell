import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ControllerFamily, detectControllerFamily } from '../controllerDisplayMapper';
import type { ControllerShortcutSettings, ControllerStatusInfo, DisplayLayoutProfile } from '../types/bridge';
import { ControllerKeycaps } from './ControllerKeycaps';
import { HotkeyField } from './HotkeyField';

type HotkeySettingsState = {
  consoleMode: string;
  pcGamingMode: string;
  workMode: string;
};

export function ControllerStep({
  stepNumber,
  pcGamingProfiles,
  selectedProfileName,
  isWorkMode = false,
  hotkeys,
  hotkeyValidationError,
  controllerShortcut,
  controllerValidationError,
  controllerStatus,
  captureSuccessButtons,
  controllerNotice,
  onStartControllerCapture,
  onCancelControllerCapture,
  onRemoveControllerShortcut,
  onRemoveHotkey,
  onChangeHotkey
}: {
  stepNumber: number;
  pcGamingProfiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  isWorkMode?: boolean;
  hotkeys: HotkeySettingsState;
  hotkeyValidationError: string | null;
  controllerShortcut: ControllerShortcutSettings | null;
  controllerValidationError: string | null;
  controllerStatus: ControllerStatusInfo;
  captureSuccessButtons: string[] | null;
  controllerNotice: 'missing' | 'disconnected' | null;
  onStartControllerCapture: () => void;
  onCancelControllerCapture: () => void;
  onRemoveControllerShortcut: () => void;
  onRemoveHotkey: () => void;
  onChangeHotkey: (key: 'enterProfile' | 'workMode', value: string) => void;
}) {
  const { t } = useTranslation();
  const [showHotkeySaved, setShowHotkeySaved] = useState(false);
  const hotkeySavedTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const hasWorkingProfile = selectedProfileName.trim().length > 0;
  const selectedProfileLabel = isWorkMode ? t('setup.workMode') : (selectedProfileName || t('profile.noProfileSelected'));
  const currentHotkey = isWorkMode ? hotkeys.workMode : hotkeys.pcGamingMode;
  const selectedControllerButtons = controllerShortcut?.buttons ?? [];
  const hasControllerShortcut = selectedControllerButtons.length > 0;
  const isCapturePhase = controllerStatus.capturePhase === 'WaitingForRelease'
    || controllerStatus.capturePhase === 'WaitingForFirstPress'
    || controllerStatus.capturePhase === 'Recording'
    || controllerStatus.capturePhase === 'WaitingForCombinationRelease';
  const canCancelCapture = controllerStatus.capturePhase === 'WaitingForRelease'
    || controllerStatus.capturePhase === 'WaitingForFirstPress';
  const isCaptureSuccess = controllerStatus.capturePhase === 'Completed' || captureSuccessButtons !== null;
  const displayControllerButtons = controllerStatus.capturedButtons.length > 0
    ? controllerStatus.capturedButtons
    : selectedControllerButtons;
  const controllerFamily = detectControllerFamily(controllerStatus.deviceName);
  const controllerDeviceLabel = controllerStatus.isConnected
    ? controllerFamily === ControllerFamily.PlayStation
      ? t('shortcut.controllerStatusConnected')
      : controllerFamily === ControllerFamily.Xbox
        ? t('shortcut.controllerStatusConnected')
        : controllerFamily === ControllerFamily.Nintendo
          ? t('shortcut.controllerStatusConnected')
          : t('shortcut.controllerStatusConnected')
    : t('shortcut.controllerStatusMissing');
  const controllerDeviceDescription = controllerStatus.deviceName || t('shortcut.controllerMissing');

  useEffect(() => {
    return () => {
      if (hotkeySavedTimerRef.current) {
        clearTimeout(hotkeySavedTimerRef.current);
      }
    };
  }, []);

  function showHotkeySavedFeedback() {
    if (hotkeySavedTimerRef.current) {
      clearTimeout(hotkeySavedTimerRef.current);
    }

    setShowHotkeySaved(true);
    hotkeySavedTimerRef.current = setTimeout(() => {
      setShowHotkeySaved(false);
      hotkeySavedTimerRef.current = null;
    }, 1800);
  }

  function handleHotkeyChange(value: string) {
    onChangeHotkey(isWorkMode ? 'workMode' : 'enterProfile', value);
    if (value.trim()) {
      showHotkeySavedFeedback();
    } else {
      setShowHotkeySaved(false);
    }
  }

  function handleHotkeyRemove() {
    setShowHotkeySaved(false);
    onRemoveHotkey();
  }

  return (
    <div className="wizard-copy profile-shortcuts-content">
      <p className="eyebrow">{t('setup.step')} {stepNumber}</p>
      <h2>{isWorkMode ? t('shortcut.workTitle') : t('shortcut.profileTitle')}</h2>
      <div className="profile-shortcuts-intro">
        <p>{isWorkMode ? t('shortcut.keyboardHelpWork') : t('shortcut.keyboardProfileHint', { name: selectedProfileLabel })}</p>
        <span className="profile-shortcuts-profile-label">{isWorkMode ? t('shortcut.workTitle').toUpperCase() : `${t('shortcut.profileTitle').toUpperCase()}: ${selectedProfileLabel.toUpperCase()}`}</span>
      </div>

      <section className="shortcut-module keyboard-shortcut-module">
        <header className="shortcut-module-header">
          <div>
            <h3>{t('shortcut.keyboardLabel')}</h3>
            <p>{isWorkMode ? t('shortcut.keyboardHelpWork') : t('shortcut.keyboardHelp')}</p>
          </div>
        </header>
        <div className="shortcut-module-body keyboard-shortcut-body">
          <div className="shortcut-control-label">
            <span>{t('shortcut.keyboardLabel').toUpperCase()}</span>
            <div className="keyboard-shortcut-row">
              <label className="keyboard-shortcut-field">
                <HotkeyField
                  value={currentHotkey}
                  onChange={handleHotkeyChange}
                  placeholder={isWorkMode ? 'np. Ctrl+Alt+W' : 'np. Ctrl+Alt+P'}
                  disabled={!isWorkMode && !hasWorkingProfile}
                  error={hotkeyValidationError}
                />
              </label>
              {showHotkeySaved ? (
                <span className="shortcut-saved-status is-saved">{t('shortcut.save')}</span>
              ) : currentHotkey ? (
                <button className="danger-button secondary-button controller-shortcut-remove" type="button" onClick={handleHotkeyRemove}>
                  {t('shortcut.remove')}
                </button>
              ) : null}
            </div>
          </div>
        </div>
      </section>

      <section className={`shortcut-module controller-shortcut-module ${isCapturePhase || isCaptureSuccess ? 'is-capturing' : ''}`}>
        <header className="shortcut-module-header controller-module-header">
          <div>
            <h3>{t('shortcut.controllerLabel')}</h3>
            <p>{t('shortcut.controllerHelp', { action: isWorkMode ? t('shortcut.workAction') : t('shortcut.profileAction') })}</p>
          </div>
          <span className={`controller-status-pill ${controllerStatus.isConnected ? 'is-connected' : 'is-disconnected'}`} title={controllerDeviceDescription}>
            {controllerDeviceLabel}
          </span>
        </header>
        <div className="shortcut-module-body controller-shortcut-body">
          {controllerNotice ? (
            <div className="inline-notice controller-device-notice" role="alert" aria-live="assertive">
              <span className="inline-notice-label">{controllerNotice === 'missing' ? t('shortcut.controllerStatusMissing') : t('shortcut.controllerStatusDisconnected')}</span>
              <div className="inline-notice-copy">
                <strong>{controllerNotice === 'missing' ? t('shortcut.controllerMissing') : t('shortcut.controllerDisconnected')}</strong>
                <span>{controllerNotice === 'missing' ? t('shortcut.controllerMissingHelp') : t('shortcut.controllerDisconnectedHelp')}</span>
                {controllerNotice === 'missing' ? <span>{t('shortcut.controllerAutoDetect')}</span> : null}
              </div>
            </div>
          ) : null}
          {isCapturePhase || isCaptureSuccess ? (
            <div className="controller-capture-panel" aria-live="polite">
              {isCaptureSuccess ? (
                <>
                  <span className="capture-mode-label capture-success-label">{t('shortcut.save')}</span>
                  <ControllerKeycaps buttons={captureSuccessButtons ?? controllerStatus.completedCapture?.buttons ?? []} family={controllerFamily} />
                </>
              ) : null}
              {controllerStatus.capturePhase === 'WaitingForRelease' ? (
                <>
                  <span className="capture-mode-label">{t('shortcut.listening')}</span>
                  <p>{t('shortcut.captureWaitingRelease')}</p>
                  <small>{t('shortcut.captureHold')}</small>
                </>
              ) : null}
              {controllerStatus.capturePhase === 'WaitingForFirstPress' ? (
                <>
                  <span className="capture-mode-label">{t('shortcut.listening')}</span>
                  <p className="capture-primary-message">{t('shortcut.captureWaitingPress')}</p>
                  <small>{t('shortcut.captureHold')}</small>
                </>
              ) : null}
              {controllerStatus.capturePhase === 'Recording' ? (
                <>
                  <span className="capture-mode-label">{t('shortcut.listening')}</span>
                  <p>{t('shortcut.captureRecording')}</p>
                  <ControllerKeycaps buttons={displayControllerButtons} family={controllerFamily} />
                  <small>{t('shortcut.captureHold')}</small>
                </>
              ) : null}
              {controllerStatus.capturePhase === 'WaitingForCombinationRelease' ? (
                <>
                  <p>{t('shortcut.captureRelease')}</p>
                  <ControllerKeycaps buttons={displayControllerButtons} family={controllerFamily} />
                </>
              ) : null}
              {canCancelCapture ? (
                <button className="secondary-button controller-capture-cancel" type="button" onClick={onCancelControllerCapture}>
                  {t('shortcut.cancel')}
                </button>
              ) : null}
            </div>
          ) : (
            <>
              {controllerStatus.capturePhase === 'Completed' && displayControllerButtons.length > 0 ? (
                <div className="controller-capture-completed" aria-live="polite">
                  <span className="capture-step-label">{t('shortcut.captureAssigned')}</span>
                  <ControllerKeycaps buttons={displayControllerButtons} family={controllerFamily} />
                </div>
              ) : hasControllerShortcut ? (
                <div className="controller-assigned-state">
                  <span className="capture-step-label">{t('shortcut.controllerSet')}</span>
                  <ControllerKeycaps buttons={selectedControllerButtons} family={controllerFamily} />
                  <p>{t('shortcut.controllerHelp', { action: isWorkMode ? t('shortcut.workAction') : t('shortcut.profileAction') })}</p>
                </div>
              ) : (
                <div className="controller-empty-state">
                  <span className="capture-step-label">{t('shortcut.controllerNone')}</span>
                  <p>{t('shortcut.controllerInputHint', { action: isWorkMode ? t('shortcut.workActionShort') : t('shortcut.profileActionShort') })}</p>
                </div>
              )}
              <div className="controller-shortcut-actions">
                <button className="primary-button controller-shortcut-listen-button" type="button" disabled={!isWorkMode && !hasWorkingProfile} onClick={onStartControllerCapture}>
                  {hasControllerShortcut ? t('shortcut.listenChange') : t('shortcut.listen')}
                </button>
                {hasControllerShortcut ? (
                  <button className="danger-button secondary-button controller-shortcut-remove" type="button" onClick={onRemoveControllerShortcut}>
                    {t('shortcut.remove')}
                  </button>
                ) : null}
              </div>
            </>
          )}
          {controllerValidationError ? <div className="field-validation-error" role="alert">{controllerValidationError}</div> : null}
          <details className="controller-diagnostics">
            <summary>{t('shortcut.captureDetails')}</summary>
            <dl>
              <div><dt>{t('shortcut.controllerCount')}</dt><dd>{controllerStatus.controllerCount}</dd></div>
              <div><dt>{t('shortcut.deviceLabel')}</dt><dd>{controllerDeviceDescription}</dd></div>
            </dl>
          </details>
        </div>
      </section>
    </div>
  );
}
