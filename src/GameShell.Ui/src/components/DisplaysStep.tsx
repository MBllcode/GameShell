import { useEffect, useState } from 'react';
import type { DisplayDeviceInfo, DisplayIdentity, DisplayLayoutProfile } from '../types/bridge';
import { normalizePcGamingProfileName } from '../features/profiles/utils';
import {
  extractDisplayIdentities,
  formatDisplayRefresh,
  formatDisplayResolution,
  MAX_PROFILE_DISPLAYS,
  PROFILE_DISPLAY_LIMIT_MESSAGE,
  sameIdentity
} from '../utils/displays';

type DisplayValidationState = {
  showNameError: boolean;
  showSelectionError: boolean;
};

export function DisplaysStep({
  stepNumber,
  displays,
  pcGamingProfiles,
  selectedProfileName,
  prefillFromSelectedProfile,
  onDraftChange,
  validationState,
  onValidationStateChange,
  isRefreshingDisplays,
  displayListError,
  refreshDisplays
}: {
  stepNumber: number;
  displays: DisplayDeviceInfo[];
  pcGamingProfiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  prefillFromSelectedProfile: boolean;
  onDraftChange: (profileName: string, identities: DisplayIdentity[]) => void;
  validationState: DisplayValidationState;
  onValidationStateChange: (state: DisplayValidationState) => void;
  isRefreshingDisplays: boolean;
  displayListError: string | null;
  refreshDisplays: () => Promise<void>;
}) {
  const [newProfileName, setNewProfileName] = useState('');
  const [selectedDisplays, setSelectedDisplays] = useState<DisplayIdentity[]>([]);
  const [displayLimitMessage, setDisplayLimitMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!prefillFromSelectedProfile || !selectedProfileName) {
      return;
    }

    const selectedProfile = pcGamingProfiles.find((profile) => profile.name === selectedProfileName) ?? null;
    if (!selectedProfile) {
      return;
    }

    const profileIdentities = extractDisplayIdentities(selectedProfile.layout);
    setNewProfileName(selectedProfile.name);
    setSelectedDisplays(profileIdentities);
    setDisplayLimitMessage(null);
    onValidationStateChange({ showNameError: false, showSelectionError: false });
    onDraftChange(selectedProfile.name, profileIdentities);
  }, [onValidationStateChange, onDraftChange, pcGamingProfiles, prefillFromSelectedProfile, selectedProfileName]);

  const canEditSelection = Boolean(newProfileName.trim()) && selectedDisplays.length > 0;

  function toggleSelectedDisplay(identity: DisplayIdentity) {
    setSelectedDisplays((current) => {
      const alreadySelected = current.some((selected) => sameIdentity(selected, identity));
      if (!alreadySelected && current.length >= MAX_PROFILE_DISPLAYS) {
        setDisplayLimitMessage(PROFILE_DISPLAY_LIMIT_MESSAGE);
        return current;
      }

      const next = alreadySelected
        ? current.filter((selected) => !sameIdentity(selected, identity))
        : [...current, identity];

      if (next.length <= MAX_PROFILE_DISPLAYS) {
        setDisplayLimitMessage(null);
      }
      if (next.length > 0 && validationState.showSelectionError) {
        onValidationStateChange({ ...validationState, showSelectionError: false });
      }

      onDraftChange(newProfileName, next);
      return next;
    });
  }

  return (
    <div className="wizard-copy displays-step">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Ekrany</h2>
      <section className="settings-section">
        <p>Wybierz ekrany używane przez profil.</p>
        <DisplayDiagnosticsPanel
          displays={displays}
          selectedDisplays={selectedDisplays}
          onToggleDisplay={toggleSelectedDisplay}
          isRefreshing={isRefreshingDisplays}
          error={displayListError}
          refreshDisplays={refreshDisplays}
          profileName={newProfileName}
          setProfileName={(nextName) => {
            const normalizedName = normalizePcGamingProfileName(nextName);
            setNewProfileName(normalizedName);
            if (normalizedName.length > 0 && validationState.showNameError) {
              onValidationStateChange({ ...validationState, showNameError: false });
            }
            onDraftChange(normalizedName, selectedDisplays);
          }}
          validationState={validationState}
          onValidationStateChange={onValidationStateChange}
          canEditSelection={canEditSelection}
          displayLimitMessage={displayLimitMessage}
        />
      </section>
    </div>
  );
}

function DisplayDiagnosticsPanel({
  displays,
  selectedDisplays,
  onToggleDisplay,
  isRefreshing,
  error,
  refreshDisplays,
  profileName,
  setProfileName,
  validationState,
  onValidationStateChange,
  canEditSelection,
  displayLimitMessage
}: {
  displays: DisplayDeviceInfo[];
  selectedDisplays: DisplayIdentity[];
  onToggleDisplay: (identity: DisplayIdentity) => void;
  isRefreshing: boolean;
  error: string | null;
  refreshDisplays: () => Promise<void>;
  profileName: string;
  setProfileName: (value: string) => void;
  validationState: DisplayValidationState;
  onValidationStateChange: (state: DisplayValidationState) => void;
  canEditSelection: boolean;
  displayLimitMessage: string | null;
}) {
  const [limitFeedbackKey, setLimitFeedbackKey] = useState<string | null>(null);
  const showNameError = validationState.showNameError && profileName.trim().length === 0;
  const showSelectionError = validationState.showSelectionError && selectedDisplays.length === 0;

  function triggerLimitFeedback(display: DisplayDeviceInfo) {
    const feedbackKey = display.id || `${display.adapterLuid}:${display.targetId}`;
    setLimitFeedbackKey(feedbackKey);
    window.setTimeout(() => {
      setLimitFeedbackKey((current) => (current === feedbackKey ? null : current));
    }, 320);
  }

  function handleDisplayToggle(display: DisplayDeviceInfo) {
    const isSelected = selectedDisplays.some((selectedDisplay) => sameIdentity(selectedDisplay, display.identity));
    if (!isSelected && selectedDisplays.length >= MAX_PROFILE_DISPLAYS) {
      triggerLimitFeedback(display);
    }
    onToggleDisplay(display.identity);
  }

  return (
    <section className="display-diagnostics display-module" aria-label="Dostępne ekrany">
      <div className="display-diagnostics-head display-module-header">
        <div><p>Wybierz dostępne ekrany i nadaj profilowi nazwę.</p></div>
        <div className="display-diagnostics-actions">
          <button className="secondary-button compact" type="button" disabled={isRefreshing} onClick={refreshDisplays}>
            {isRefreshing ? 'Odświeżanie...' : 'Odśwież'}
          </button>
        </div>
      </div>
      <div className="display-module-body">
        <div className="profile-name-section">
          <div className="profile-name-header">
            <label className="profile-name-label" htmlFor="profile-name-input">Nazwa profilu</label>
            <span className="selection-count" aria-live="polite">Ekrany <strong>{selectedDisplays.length}/{MAX_PROFILE_DISPLAYS}</strong></span>
          </div>
          <input
            id="profile-name-input"
            className="text-input profile-name-input"
            type="text"
            value={profileName}
            onChange={(event) => setProfileName(event.target.value)}
            onBlur={() => onValidationStateChange({ ...validationState, showNameError: profileName.trim().length === 0 })}
            aria-invalid={showNameError}
            aria-describedby={showNameError ? 'profile-name-validation-error' : undefined}
            placeholder="np. Salon TV + biurko"
          />
        </div>
        {showNameError ? <div id="profile-name-validation-error" className="field-validation-error" role="alert">Wpisz nazwę profilu.</div> : null}
        {displayLimitMessage ? (
          <div className="inline-notice inline-notice-warning" role="alert" aria-live="assertive">
            <span className="inline-notice-label">LIMIT</span>
            <div className="inline-notice-copy"><strong>{displayLimitMessage}</strong><span>Profil może obejmować najwyżej dwa ekrany.</span></div>
          </div>
        ) : null}
        {showSelectionError ? <div className="selection-validation-error" role="alert">Wybierz przynajmniej jeden ekran.</div> : null}
        {error ? <p className="error-line display-error">{error}</p> : null}
        {!error && displays.length === 0 ? <p className="hint-line">Nie znaleziono ekranów. Podłącz ekran i odśwież listę.</p> : null}
        {displays.length > 0 ? (
          <div className="display-grid">
            {displays.map((display) => {
              const isSelected = selectedDisplays.some((selectedDisplay) => sameIdentity(selectedDisplay, display.identity));
              const feedbackKey = display.id || `${display.adapterLuid}:${display.targetId}`;
              const displayClasses = ['display-card', display.isEnabled ? 'is-available' : 'is-inactive', isSelected ? 'is-selected' : '', limitFeedbackKey === feedbackKey ? 'limit-hit' : ''].filter(Boolean).join(' ');

              return (
                <article
                  className={displayClasses}
                  key={feedbackKey}
                  role="button"
                  tabIndex={0}
                  aria-pressed={isSelected}
                  aria-label={`Ekran ${display.name || 'Nieznany ekran'}`}
                  onClick={() => handleDisplayToggle(display)}
                  onKeyDown={(event) => {
                    if (event.key !== 'Enter' && event.key !== ' ') return;
                    event.preventDefault();
                    handleDisplayToggle(display);
                  }}
                >
                  <div className="display-card-title-line">
                    <strong>{display.name || 'Nieznany ekran'}</strong>
                    <div className="display-card-badges">
                      {display.isEnabled && display.isPrimary ? <span className="display-state-badge">GŁÓWNY</span> : null}
                      {!display.isEnabled ? <span className="display-state-badge inactive">NIEAKTYWNY</span> : null}
                    </div>
                  </div>
                  <span className="display-tech-line">{formatDisplayResolution(display)} · {formatDisplayRefresh(display)}</span>
                  {display.connectionTechnology ? <small>{display.connectionTechnology}</small> : null}
                  <small>Pozycja: {display.positionX}, {display.positionY}</small>
                </article>
              );
            })}
          </div>
        ) : null}
      </div>
    </section>
  );
}
