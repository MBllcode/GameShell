import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ControllerFamily, detectControllerFamily } from '../../../controllerDisplayMapper';
import type { ControllerShortcutSettings, ControllerStatusInfo } from '../../../bridge/contracts';
import { ControllerKeycaps } from './ControllerKeycaps';
import { HotkeyField } from './HotkeyField';
import { IconBadge, PageHeader, UiIcon } from '../../../shared/ui/UiPrimitives';

type HotkeySettingsState = {
  consoleMode: string;
  gamingMode: string;
  workMode: string;
};

export function ControllerStep({
  stepNumber,
  selectedProfileName,
  isWorkMode = false,
  hotkeys,
  hotkeyValidationError,
  onHotkeyValidationErrorChange,
  controllerShortcut,
  controllerValidationError,
  controllerStatus,
  captureSuccessButtons,
  rejectedControllerButtons,
  onControllerValidationErrorChange,
  controllerNotice,
  onStartControllerCapture,
  onCancelControllerCapture,
  onRemoveControllerShortcut,
  onRemoveHotkey,
  onChangeHotkey
}: {
  stepNumber: number;
  selectedProfileName: string;
  isWorkMode?: boolean;
  hotkeys: HotkeySettingsState;
  hotkeyValidationError: string | null;
  onHotkeyValidationErrorChange: (value: string | null) => void;
  controllerShortcut: ControllerShortcutSettings | null;
  controllerValidationError: string | null;
  controllerStatus: ControllerStatusInfo;
  captureSuccessButtons: string[] | null;
  rejectedControllerButtons: string[] | null;
  onControllerValidationErrorChange: (value: string | null) => void;
  controllerNotice: 'missing' | 'disconnected' | null;
  onStartControllerCapture: () => void;
  onCancelControllerCapture: () => void;
  onRemoveControllerShortcut: () => void;
  onRemoveHotkey: () => void;
  onChangeHotkey: (key: 'enterProfile' | 'workMode', value: string) => boolean;
}) {
  const { t } = useTranslation();
  const [pendingHotkeyCandidate, setPendingHotkeyCandidate] = useState<string | null>(null);
  const [visibleHotkeyValidationError, setVisibleHotkeyValidationError] = useState<string | null>(null);
  const [visibleControllerValidationError, setVisibleControllerValidationError] = useState<string | null>(null);
  const hasWorkingProfile = selectedProfileName.trim().length > 0;
  const selectedProfileLabel = isWorkMode ? t('setup.workMode') : (selectedProfileName || t('profile.noProfileSelected'));
  const currentHotkey = isWorkMode ? hotkeys.workMode : hotkeys.gamingMode;
  const displayedHotkey = pendingHotkeyCandidate ?? currentHotkey;
  const selectedControllerButtons = controllerShortcut?.buttons ?? [];
  const hasControllerShortcut = selectedControllerButtons.length > 0;
  const hasControllerValidationError = Boolean(visibleControllerValidationError);
  const hasRejectedControllerCandidate = hasControllerValidationError && rejectedControllerButtons !== null;
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
  const displayedControllerButtons = rejectedControllerButtons ?? displayControllerButtons;
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
    if (!hotkeyValidationError) {
      setVisibleHotkeyValidationError(null);
      return;
    }

    setVisibleHotkeyValidationError(hotkeyValidationError);
    const timeoutId = setTimeout(() => {
      setPendingHotkeyCandidate(null);
      setVisibleHotkeyValidationError(null);
      onHotkeyValidationErrorChange(null);
    }, 2800);

    return () => clearTimeout(timeoutId);
  }, [hotkeyValidationError, onHotkeyValidationErrorChange]);

  useEffect(() => {
    if (!controllerValidationError) {
      setVisibleControllerValidationError(null);
      return;
    }

    setVisibleControllerValidationError(controllerValidationError);
    const timeoutId = setTimeout(() => {
      setVisibleControllerValidationError(null);
      onControllerValidationErrorChange(null);
    }, 2800);

    return () => clearTimeout(timeoutId);
  }, [controllerValidationError, onControllerValidationErrorChange]);

  function handleHotkeyChange(value: string) {
    setPendingHotkeyCandidate(value);
    const saved = onChangeHotkey(isWorkMode ? 'workMode' : 'enterProfile', value);
    if (saved && value.trim()) {
      setPendingHotkeyCandidate(null);
    } else if (saved) {
      setPendingHotkeyCandidate(null);
    }
  }

  function handleHotkeyRemove() {
    setPendingHotkeyCandidate(null);
    onRemoveHotkey();
  }

  return (
    <div className="wizard-copy profile-shortcuts-content">
      <PageHeader eyebrow={`${t('setup.step')} ${stepNumber}`} title={isWorkMode ? t('shortcut.workTitle') : t('shortcut.profileTitle')} description={isWorkMode ? t('shortcut.keyboardHelpWork') : t('shortcut.keyboardProfileHint', { name: selectedProfileLabel })} />
      <div className="profile-shortcuts-intro">
        <span className="profile-shortcuts-profile-label">{isWorkMode ? t('shortcut.workTitle').toUpperCase() : `${t('shortcut.profileTitle').toUpperCase()}: ${selectedProfileLabel.toUpperCase()}`}</span>
      </div>

      <section className="shortcut-module keyboard-shortcut-module">
        <header className="shortcut-module-header">
          <IconBadge name="keyboard" size={18} />
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
                  value={displayedHotkey}
                  onChange={handleHotkeyChange}
                  placeholder={isWorkMode ? 'np. Ctrl+Alt+W' : 'np. Ctrl+Alt+P'}
                  disabled={!isWorkMode && !hasWorkingProfile}
                  error={visibleHotkeyValidationError}
                />
              </label>
              <div className="shortcut-action-slot">
                {currentHotkey ? (
                  <button className="danger-button secondary-button controller-shortcut-remove" type="button" onClick={handleHotkeyRemove}>
                    <UiIcon name="trash" size={15} />
                    {t('shortcut.remove')}
                  </button>
                ) : null}
              </div>
            </div>
          </div>
        </div>
      </section>

      <section className={`shortcut-module controller-shortcut-module ${isCapturePhase || isCaptureSuccess ? 'is-capturing' : ''}`}>
        <header className="shortcut-module-header controller-module-header">
          <IconBadge name="controller" size={18} />
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
                  <ControllerKeycaps buttons={captureSuccessButtons ?? controllerStatus.completedCapture?.buttons ?? []} family={controllerFamily} hasError={hasRejectedControllerCandidate} />
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
                  <UiIcon name="warning" size={15} />
                  {t('shortcut.cancel')}
                </button>
              ) : null}
            </div>
          ) : (
            <>
              {hasRejectedControllerCandidate ? (
                <div className="controller-capture-completed controller-capture-invalid" aria-live="polite">
                  <span className="capture-step-label">{t('shortcut.controllerLabel')}</span>
                  <ControllerKeycaps buttons={displayedControllerButtons} family={controllerFamily} hasError />
                </div>
              ) : controllerStatus.capturePhase === 'Completed' && displayControllerButtons.length > 0 ? (
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
                  <UiIcon name="controller" size={15} />
                  {hasControllerShortcut ? t('shortcut.listenChange') : t('shortcut.listen')}
                </button>
                {hasControllerShortcut ? (
                  <button className="danger-button secondary-button controller-shortcut-remove" type="button" onClick={onRemoveControllerShortcut}>
                    <UiIcon name="trash" size={15} />
                    {t('shortcut.remove')}
                  </button>
                ) : null}
              </div>
            </>
          )}
          {visibleControllerValidationError ? <div className="field-validation-error" role="alert">{visibleControllerValidationError}</div> : null}
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
