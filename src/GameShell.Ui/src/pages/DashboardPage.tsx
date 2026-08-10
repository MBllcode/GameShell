import { useEffect, useState } from 'react';
import type { AppStatus, DisplayLayoutProfile, DisplayLayoutSnapshot } from '../types/bridge';
import { summarizeLayout } from '../utils/displays';
import { formatCurrentMode, formatSessionState } from '../utils/status';

type LauncherType = 'Playnite Fullscreen' | 'Steam Big Picture' | 'Xbox App';
type ModeCommand = 'enterGameShell' | 'enterGameShellWithProfile' | 'enterWorkMode';
type StepId = 'gaming-display' | 'launcher' | 'console-behaviour' | 'controller' | 'work-behaviour' | 'work-shortcuts' | 'app-behaviour';
type ProfileHotkeysState = { enterProfile: string };
type PcGamingProfileLauncherMap = Record<string, LauncherType>;
type PcGamingProfileHotkeysMap = Record<string, ProfileHotkeysState>;

export type DashboardPageProps = {
  status: AppStatus;
  launcher: LauncherType;
  pcGamingLayout: DisplayLayoutSnapshot | null;
  pcGamingProfiles: DisplayLayoutProfile[];
  pcGamingProfileLaunchers: PcGamingProfileLauncherMap;
  pcGamingProfileHotkeys: PcGamingProfileHotkeysMap;
  activeProfileName: string | null;
  selectedProfileName: string;
  globalWorkModeHotkey: string;
  isModeSwitching: boolean;
  isRemovingProfile: boolean;
  activeModeAction: string | null;
  message: string;
  hasError: boolean;
  runModeCommand: (command: ModeCommand, saveBeforeRun?: boolean, payload?: unknown) => Promise<void>;
  deleteSelectedProfile: () => Promise<void>;
  selectProfile: (profileName: string) => void;
  createProfile: () => void;
  editSettings: (stepId: StepId) => void;
};

export function DashboardPage({
  status,
  launcher,
  pcGamingLayout,
  pcGamingProfiles,
  pcGamingProfileLaunchers,
  pcGamingProfileHotkeys,
  activeProfileName,
  selectedProfileName,
  globalWorkModeHotkey,
  isModeSwitching,
  isRemovingProfile,
  activeModeAction,
  message,
  hasError,
  runModeCommand,
  deleteSelectedProfile,
  selectProfile,
  createProfile,
  editSettings
}: DashboardPageProps) {
  const [showDeleteProfileDialog, setShowDeleteProfileDialog] = useState(false);
  const dashboardSelectedProfileName = selectedProfileName || activeProfileName || pcGamingProfiles[0]?.name || '';
  const runtimeActiveProfileName = status.activeProfileName;
  const isWorkModeActive = status.currentMode === 'Work';
  const isPcGamingActive = status.currentMode === 'PcGaming';
  const isConsoleModeActive = status.currentMode === 'Console';
  const activeGamingProfileLayout = runtimeActiveProfileName
    ? pcGamingProfiles.find((profile) => profile.name === runtimeActiveProfileName)?.layout ?? null
    : null;
  const resolvedGamingLayout = activeGamingProfileLayout ?? pcGamingLayout;
  const visibleLayoutSummary = isWorkModeActive
    ? 'Układ roboczy'
    : isPcGamingActive
      ? resolvedGamingLayout ? summarizeLayout(resolvedGamingLayout) : 'Brak zapisanego profilu'
      : isConsoleModeActive
        ? status.selectedConsoleDisplay ?? 'Brak wybranego ekranu Console Mode'
        : pcGamingLayout ? summarizeLayout(pcGamingLayout) : 'Brak zapisanego profilu';
  const shouldShowLauncherFact = !isWorkModeActive;
  const visibleLauncher = status.activeLauncher ?? 'Brak aktywnego launchera';
  const selectedProfile = pcGamingProfiles.find((profile) => profile.name === dashboardSelectedProfileName) ?? null;
  const canRunSelectedProfile = Boolean(dashboardSelectedProfileName) && !isModeSwitching && !isRemovingProfile;
  const canDeleteSelectedProfile = Boolean(dashboardSelectedProfileName) && !isModeSwitching && !isRemovingProfile;
  const profileCountLabel = pcGamingProfiles.length === 1 ? 'profil' : pcGamingProfiles.length >= 2 && pcGamingProfiles.length <= 4 ? 'profile' : 'profili';

  useEffect(() => {
    console.info('[DASHBOARD RUNTIME STATE]', {
      timestamp: new Date().toISOString(),
      currentMode: status.currentMode,
      sessionState: status.sessionState,
      activeProfileName: status.activeProfileName,
      activeLauncher: status.activeLauncher
    });
  }, [status.currentMode, status.sessionState, status.activeProfileName, status.activeLauncher]);

  return (
    <main className="app-shell">
      <section className="setup-window" aria-labelledby="app-title">
        <aside className="brand-panel dashboard-sidebar" aria-label="Nawigacja aplikacji">
          <section className="sidebar-state-block" aria-label="Stan aplikacji">
            <div className="sidebar-section-label">Stan</div>
            {isWorkModeActive ? (
              <div className="sidebar-state-content">
                <div className="status-row"><strong>Work Mode</strong><span className="work-return-status">AKTYWNY</span></div>
                <p>Układ roboczy</p><small>{globalWorkModeHotkey || 'Brak skrótu'}</small>
              </div>
            ) : (
              <div className="sidebar-state-content">
                <div className="status-row"><strong>{formatCurrentMode(status)}</strong>{isPcGamingActive ? <span className="work-return-status">AKTYWNY</span> : null}</div>
                {isPcGamingActive ? <p>Profil: {runtimeActiveProfileName || 'Nie wybrano'}</p> : null}
                {status.sessionState && status.sessionState !== 'Idle' ? <p className="sidebar-session-state">Sesja: {formatSessionState(status.sessionState)}</p> : null}
                <p className="sidebar-state-detail">{visibleLayoutSummary}</p>
                {shouldShowLauncherFact ? <small>{visibleLauncher}</small> : null}
                {!isConsoleModeActive && !isPcGamingActive ? <small>{globalWorkModeHotkey || 'Brak skrótu'}</small> : null}
                {!isWorkModeActive ? <button className="sidebar-state-action" type="button" disabled={isModeSwitching} onClick={() => runModeCommand('enterWorkMode')}>Wróć do Work Mode</button> : null}
              </div>
            )}
          </section>
          <section className="sidebar-global-settings" aria-label="Globalne ustawienia">
            <div className="sidebar-section-label">Ustawienia globalne</div>
            <div className="sidebar-global-links">
              <button type="button" onClick={() => editSettings('work-behaviour')}><span>Zachowanie Work Mode</span></button>
              <button type="button" onClick={() => editSettings('work-shortcuts')}><span>Skróty Work Mode</span></button>
              <button type="button" onClick={() => editSettings('app-behaviour')}><span>Uruchamianie aplikacji</span></button>
            </div>
          </section>
        </aside>

        <div className="setup-main dashboard-main">
          <section className="setup-page dashboard-page">
            <div className="dashboard-layout">
              <section className="dashboard-band dashboard-mode-band" aria-labelledby="dashboard-modes-title">
                <section className="quick-switch-panel profile-module" aria-label="Zapisane profile ekranów">
                  <div className="profile-module-header">
                    <div className="profile-module-title-wrap"><h1 id="app-title" className="profile-module-title">Profile gamingowe</h1></div>
                    <div className="profile-module-header-actions">
                      <small className="profile-module-count">{pcGamingProfiles.length} {profileCountLabel}</small>
                      <button className="secondary-button compact add-profile-head-button" type="button" onClick={createProfile} disabled={isRemovingProfile}>+ Dodaj profil</button>
                    </div>
                  </div>
                  <div className="profile-list-shell">
                    <div className="profile-quick-list" role="listbox" aria-label="Lista profili gamingowych">
                      {pcGamingProfiles.map((profile) => {
                        const profileLauncher = pcGamingProfileLaunchers[profile.name] ?? launcher;
                        const isProfileConfiguredAsActive = runtimeActiveProfileName === profile.name;
                        const isProfileSelectedForEditing = dashboardSelectedProfileName === profile.name;
                        const isPcGamingNow = status.currentMode === 'PcGaming';
                        const stateLabel = isPcGamingNow && isProfileConfiguredAsActive ? 'AKTYWNY' : isProfileSelectedForEditing ? 'ZAZNACZONY' : 'GOTOWY';
                        return (
                          <button className={`profile-quick-row ${isProfileSelectedForEditing ? 'selected' : ''} ${isPcGamingNow && isProfileConfiguredAsActive ? 'active' : ''}`} type="button" key={profile.name} disabled={isModeSwitching || isRemovingProfile} onClick={() => selectProfile(profile.name)} role="option" aria-selected={isProfileSelectedForEditing}>
                            <div className="profile-quick-main"><strong>{profile.name}</strong><small>{summarizeLayout(profile.layout)} · {profileLauncher}</small></div>
                            <div className="profile-quick-meta"><span className="profile-quick-state">{stateLabel}</span></div>
                          </button>
                        );
                      })}
                    </div>
                  </div>
                  <div className="profile-actions profile-module-footer">
                    <button className="danger-button secondary-button compact delete-action" type="button" disabled={!canDeleteSelectedProfile} onClick={() => setShowDeleteProfileDialog(true)}>Usuń profil</button>
                    <button className="primary-button launch-profile-button launch-action" type="button" disabled={!canRunSelectedProfile} onClick={() => runModeCommand('enterGameShellWithProfile', false, { profileName: dashboardSelectedProfileName })}><span>Uruchom profil{selectedProfile ? `: ${selectedProfile.name}` : ''}</span></button>
                  </div>
                </section>
                {pcGamingProfiles.length === 0 ? <p className="hint-line">Nie masz jeszcze profilu. Dodaj go w sekcji Ekrany.</p> : null}
              </section>
              <section className="dashboard-band dashboard-config-section" aria-label="Ustawienia profilu">
                <div className="dashboard-shortcut-groups" aria-label="Edycja ustawień">
                  <section className="dashboard-shortcut-group" aria-labelledby="profile-settings-title">
                    <div className="profile-settings-heading"><h3 id="profile-settings-title">USTAWIENIA PROFILU</h3><span className="profile-context">{dashboardSelectedProfileName || 'PC'}</span></div>
                    <div className="section-shortcuts">
                      <button type="button" onClick={() => editSettings('gaming-display')}><span>Ekrany</span><small>Wybierz ekrany używane przez profil.</small></button>
                      <button type="button" onClick={() => editSettings('launcher')}><span>Launcher profilu</span><small>Wybierz aplikację uruchamianą z profilem.</small></button>
                      <button type="button" onClick={() => editSettings('console-behaviour')}><span>Zachowanie profilu</span><small>Ustaw zachowanie pulpitu po uruchomieniu profilu.</small></button>
                      <button type="button" onClick={() => editSettings('controller')}><span>Skróty profilu</span><small>Ustaw skróty uruchamiające profil.</small></button>
                    </div>
                  </section>
                </div>
              </section>
            </div>
          </section>
          <footer className="setup-footer"><p className={hasError ? 'error-line' : 'operation-line'}>{message}</p></footer>
          {showDeleteProfileDialog ? (
            <div className="dialog-backdrop" role="presentation"><div className="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="delete-profile-dialog-title">
              <h3 id="delete-profile-dialog-title">Usuwanie profilu</h3>
              <p>Czy na pewno chcesz usunąć profil{selectedProfile ? ` „${selectedProfile.name}”` : ''}?</p>
              <div className="dialog-actions">
                <button className="secondary-button" type="button" disabled={isRemovingProfile} onClick={() => setShowDeleteProfileDialog(false)}>Anuluj</button>
                <button className="danger-button secondary-button" type="button" disabled={isRemovingProfile} onClick={() => { void (async () => { await deleteSelectedProfile(); setShowDeleteProfileDialog(false); })(); }}>{isRemovingProfile ? 'Usuwanie...' : 'Usuń profil'}</button>
              </div>
            </div></div>
          ) : null}
        </div>
      </section>
    </main>
  );
}
