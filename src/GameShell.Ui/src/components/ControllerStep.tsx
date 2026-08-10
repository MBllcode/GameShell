import { useEffect, useRef, useState } from 'react';
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
  const [showHotkeySaved, setShowHotkeySaved] = useState(false);
  const hotkeySavedTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const hasWorkingProfile = selectedProfileName.trim().length > 0;
  const selectedProfileLabel = selectedProfileName || 'Brak zaznaczonego profilu';
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
      ? 'PLAYSTATION POŁĄCZONY'
      : controllerFamily === ControllerFamily.Xbox
        ? 'XBOX POŁĄCZONY'
        : controllerFamily === ControllerFamily.Nintendo
          ? 'NINTENDO POŁĄCZONY'
          : 'KONTROLER POŁĄCZONY'
    : 'BRAK KONTROLERA';
  const controllerDeviceDescription = controllerStatus.deviceName || 'Brak wykrytego kontrolera';

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
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>{isWorkMode ? 'Skrót Work Mode' : 'Skróty profilu'}</h2>
      <div className="profile-shortcuts-intro">
        <p>{isWorkMode ? 'Ustaw skrót powrotu do Work Mode.' : `Ustaw skrót uruchamiający profil ${selectedProfileLabel}.`}</p>
        <span className="profile-shortcuts-profile-label">{isWorkMode ? 'CEL: WORK MODE' : `PROFIL: ${selectedProfileLabel.toUpperCase()}`}</span>
      </div>

      <section className="shortcut-module keyboard-shortcut-module">
        <header className="shortcut-module-header">
          <div>
            <h3>Skrót klawiaturowy</h3>
            <p>{isWorkMode ? 'Wraca do Work Mode z dowolnego miejsca.' : 'Uruchamia profil z dowolnego miejsca.'}</p>
          </div>
        </header>
        <div className="shortcut-module-body keyboard-shortcut-body">
          <div className="shortcut-control-label">
            <span>SKRÓT</span>
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
                <span className="shortcut-saved-status is-saved">ZAPISANO</span>
              ) : currentHotkey ? (
                <button className="danger-button secondary-button controller-shortcut-remove" type="button" onClick={handleHotkeyRemove}>
                  Usuń
                </button>
              ) : null}
            </div>
          </div>
        </div>
      </section>

      <section className={`shortcut-module controller-shortcut-module ${isCapturePhase || isCaptureSuccess ? 'is-capturing' : ''}`}>
        <header className="shortcut-module-header controller-module-header">
          <div>
            <h3>Skrót kontrolera</h3>
            <p>Przytrzymaj kombinację, aby {isWorkMode ? 'wrócić do Work Mode' : 'uruchomić profil'}.</p>
          </div>
          <span className={`controller-status-pill ${controllerStatus.isConnected ? 'is-connected' : 'is-disconnected'}`} title={controllerDeviceDescription}>
            {controllerDeviceLabel}
          </span>
        </header>
        <div className="shortcut-module-body controller-shortcut-body">
          {controllerNotice ? (
            <div className="inline-notice controller-device-notice" role="alert" aria-live="assertive">
              <span className="inline-notice-label">{controllerNotice === 'missing' ? 'URZĄDZENIE' : 'ROZŁĄCZONO'}</span>
              <div className="inline-notice-copy">
                <strong>{controllerNotice === 'missing' ? 'Nie wykryto kontrolera.' : 'Kontroler został rozłączony.'}</strong>
                <span>{controllerNotice === 'missing' ? 'Podłącz kontroler przez USB lub Bluetooth i spróbuj ponownie.' : 'Podłącz go ponownie, aby kontynuować.'}</span>
                {controllerNotice === 'missing' ? <span>Aplikacja wykryje go automatycznie.</span> : null}
              </div>
            </div>
          ) : null}
          {isCapturePhase || isCaptureSuccess ? (
            <div className="controller-capture-panel" aria-live="polite">
              {isCaptureSuccess ? (
                <>
                  <span className="capture-mode-label capture-success-label">ZAPISANO</span>
                  <ControllerKeycaps buttons={captureSuccessButtons ?? controllerStatus.completedCapture?.buttons ?? []} family={controllerFamily} />
                </>
              ) : null}
              {controllerStatus.capturePhase === 'WaitingForRelease' ? (
                <>
                  <span className="capture-mode-label">NASŁUCHIWANIE</span>
                  <p>Puść wszystkie przyciski kontrolera.</p>
                  <small>Możesz teraz zwolnić przyciski.</small>
                </>
              ) : null}
              {controllerStatus.capturePhase === 'WaitingForFirstPress' ? (
                <>
                  <span className="capture-mode-label">NASŁUCHIWANIE</span>
                  <p className="capture-primary-message">Naciśnij i przytrzymaj wybraną kombinację.</p>
                  <small>Możesz naciskać przyciski po kolei.</small>
                </>
              ) : null}
              {controllerStatus.capturePhase === 'Recording' ? (
                <>
                  <span className="capture-mode-label">NASŁUCHIWANIE</span>
                  <p>Wykryto kombinację</p>
                  <ControllerKeycaps buttons={displayControllerButtons} family={controllerFamily} />
                  <small>Przytrzymaj ją jeszcze chwilę.</small>
                </>
              ) : null}
              {controllerStatus.capturePhase === 'WaitingForCombinationRelease' ? (
                <>
                  <p>Puść kombinację, aby ją zapisać.</p>
                  <ControllerKeycaps buttons={displayControllerButtons} family={controllerFamily} />
                </>
              ) : null}
              {canCancelCapture ? (
                <button className="secondary-button controller-capture-cancel" type="button" onClick={onCancelControllerCapture}>
                  Anuluj
                </button>
              ) : null}
            </div>
          ) : (
            <>
              {controllerStatus.capturePhase === 'Completed' && displayControllerButtons.length > 0 ? (
                <div className="controller-capture-completed" aria-live="polite">
                  <span className="capture-step-label">KOMBINACJA ZAPISANA</span>
                  <ControllerKeycaps buttons={displayControllerButtons} family={controllerFamily} />
                </div>
              ) : hasControllerShortcut ? (
                <div className="controller-assigned-state">
                  <span className="capture-step-label">PRZYPISANY SKRÓT</span>
                  <ControllerKeycaps buttons={selectedControllerButtons} family={controllerFamily} />
                  <p>Przytrzymaj przez {((controllerShortcut?.holdDurationMs ?? 700) / 1000).toFixed(1)} s, aby {isWorkMode ? 'przejść w Work Mode' : 'uruchomić profil'}.</p>
                </div>
              ) : (
                <div className="controller-empty-state">
                  <span className="capture-step-label">BRAK PRZYPISANEGO SKRÓTU</span>
                  <p>Ustaw przyciski, których przytrzymanie {isWorkMode ? 'wróci do Work Mode' : 'uruchomi profil'}.</p>
                </div>
              )}
              <div className="controller-shortcut-actions">
                <button className="primary-button controller-shortcut-listen-button" type="button" disabled={!isWorkMode && !hasWorkingProfile} onClick={onStartControllerCapture}>
                  {hasControllerShortcut ? 'Zmień kombinację' : 'Przypisz kombinację'}
                </button>
                {hasControllerShortcut ? (
                  <button className="danger-button secondary-button controller-shortcut-remove" type="button" onClick={onRemoveControllerShortcut}>
                    Usuń
                  </button>
                ) : null}
              </div>
            </>
          )}
          {controllerValidationError ? <div className="field-validation-error" role="alert">{controllerValidationError}</div> : null}
          <details className="controller-diagnostics">
            <summary>Szczegóły diagnostyczne</summary>
            <dl>
              <div><dt>Kontrolery</dt><dd>{controllerStatus.controllerCount}</dd></div>
              <div><dt>Urządzenie</dt><dd>{controllerDeviceDescription}</dd></div>
            </dl>
          </details>
        </div>
      </section>
    </div>
  );
}
