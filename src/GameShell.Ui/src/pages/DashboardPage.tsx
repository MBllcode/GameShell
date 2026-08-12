import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation();
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
    ? t('dashboard.workModeLayout')
    : isPcGamingActive
      ? resolvedGamingLayout ? summarizeLayout(resolvedGamingLayout, t) : t('summary.layoutMissing')
      : isConsoleModeActive
        ? status.selectedConsoleDisplay ?? t('dashboard.consoleDisplayMissing')
        : pcGamingLayout ? summarizeLayout(pcGamingLayout, t) : t('summary.layoutMissing');
  const shouldShowLauncherFact = !isWorkModeActive;
  const visibleLauncher = status.activeLauncher ?? t('dashboard.noActiveLauncher');
  const selectedProfile = pcGamingProfiles.find((profile) => profile.name === dashboardSelectedProfileName) ?? null;
  const canRunSelectedProfile = Boolean(dashboardSelectedProfileName) && !isModeSwitching && !isRemovingProfile;
  const canDeleteSelectedProfile = Boolean(dashboardSelectedProfileName) && !isModeSwitching && !isRemovingProfile;
  const profileCountLabel = pcGamingProfiles.length === 0
    ? t('dashboard.profileCount.zero')
    : pcGamingProfiles.length === 1
      ? t('dashboard.profileCount.one')
      : pcGamingProfiles.length >= 2 && pcGamingProfiles.length <= 4
        ? t('dashboard.profileCount.few')
        : t('dashboard.profileCount.many');

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
        <aside className="brand-panel dashboard-sidebar" aria-label={t('app.globalSettings')}>
          <section className="sidebar-state-block" aria-label={t('app.status')}>
            <div className="sidebar-section-label">{t('app.status')}</div>
            {isWorkModeActive ? (
              <div className="sidebar-state-content">
                <div className="status-row"><strong>{t('app.workMode')}</strong><span className="work-return-status">{t('app.active')}</span></div>
                <p className="sidebar-status-description">{t('dashboard.workModeDescription')}</p>
                <small>{globalWorkModeHotkey || t('app.missingHotkey')}</small>
              </div>
            ) : (
              <div className="sidebar-state-content">
                <div className="status-row"><strong>{formatCurrentMode(status, t)}</strong>{isPcGamingActive ? <span className="work-return-status">{t('app.active')}</span> : null}</div>
                {isPcGamingActive ? <p>{t('profile.activeProfile')}: {runtimeActiveProfileName || t('summary.activeProfileNone')}</p> : null}
                {status.sessionState && status.sessionState !== 'Idle' ? <p className="sidebar-session-state">{t('app.sessionState', { state: formatSessionState(status.sessionState, t) })}</p> : null}
                <p className="sidebar-state-detail">{visibleLayoutSummary}</p>
                {shouldShowLauncherFact ? <small>{visibleLauncher}</small> : null}
                {!isConsoleModeActive && !isPcGamingActive ? <small>{globalWorkModeHotkey || t('app.missingHotkey')}</small> : null}
                {!isWorkModeActive ? <button className="sidebar-state-action" type="button" disabled={isModeSwitching} onClick={() => runModeCommand('enterWorkMode')}>{t('dashboard.switchToWorkMode')}</button> : null}
              </div>
            )}
          </section>
          <section className="sidebar-global-settings" aria-label={t('app.globalSettings')}>
            <div className="sidebar-section-label">{t('app.globalSettings')}</div>
            <div className="sidebar-global-links">
              <button type="button" onClick={() => editSettings('work-behaviour')}><span>{t('setup.workMode')}</span></button>
              <button type="button" onClick={() => editSettings('work-shortcuts')}><span>{t('setup.workModeShortcuts')}</span></button>
              <button type="button" onClick={() => editSettings('app-behaviour')}><span>{t('setup.appBehaviour')}</span></button>
            </div>
          </section>
        </aside>

        <div className="setup-main dashboard-main">
          <section className="setup-page dashboard-page">
            <div className="dashboard-layout">
              <section className="dashboard-band dashboard-mode-band" aria-labelledby="dashboard-modes-title">
                <section className="quick-switch-panel profile-module" aria-label={t('dashboard.profiles')}>
                  <div className="profile-module-header">
                    <div className="profile-module-title-wrap"><h1 id="app-title" className="profile-module-title">{t('dashboard.profiles')}</h1></div>
                    <div className="profile-module-header-actions">
                      <small className="profile-module-count">{pcGamingProfiles.length} {profileCountLabel}</small>
                      <button className="secondary-button compact add-profile-head-button" type="button" onClick={createProfile} disabled={isRemovingProfile}>+ {t('dashboard.addProfile')}</button>
                    </div>
                  </div>
                  <div className="profile-list-shell">
                    <div className="profile-quick-list" role="listbox" aria-label={t('dashboard.profiles')}>
                      {pcGamingProfiles.map((profile) => {
                        const profileLauncher = pcGamingProfileLaunchers[profile.name] ?? launcher;
                        const isProfileConfiguredAsActive = runtimeActiveProfileName === profile.name;
                        const isProfileSelectedForEditing = dashboardSelectedProfileName === profile.name;
                        const isPcGamingNow = status.currentMode === 'PcGaming';
                        const stateLabel = isPcGamingNow && isProfileConfiguredAsActive ? t('app.active') : isProfileSelectedForEditing ? t('app.selected') : t('app.ready');
                        return (
                          <button className={`profile-quick-row ${isProfileSelectedForEditing ? 'selected' : ''} ${isPcGamingNow && isProfileConfiguredAsActive ? 'active' : ''}`} type="button" key={profile.name} disabled={isModeSwitching || isRemovingProfile} onClick={() => selectProfile(profile.name)} role="option" aria-selected={isProfileSelectedForEditing}>
                            <div className="profile-quick-main"><strong>{profile.name}</strong><small>{summarizeLayout(profile.layout, t)} · {profileLauncher}</small></div>
                            <div className="profile-quick-meta"><span className="profile-quick-state">{stateLabel}</span></div>
                          </button>
                        );
                      })}
                    </div>
                  </div>
                  <div className="profile-actions profile-module-footer">
                    <button className="danger-button secondary-button compact delete-action" type="button" disabled={!canDeleteSelectedProfile} onClick={() => setShowDeleteProfileDialog(true)}>{t('dashboard.deleteProfile')}</button>
                    <button className="primary-button launch-profile-button launch-action" type="button" disabled={!canRunSelectedProfile} onClick={() => runModeCommand('enterGameShellWithProfile', false, { profileName: dashboardSelectedProfileName })}><span>{t('dashboard.runProfile')}{selectedProfile ? `: ${selectedProfile.name}` : ''}</span></button>
                  </div>
                </section>
                {pcGamingProfiles.length === 0 ? <p className="hint-line">{t('dashboard.noProfile')}</p> : null}
              </section>
              <section className="dashboard-band dashboard-config-section" aria-label={t('dashboard.profileSettings')}>
                <div className="dashboard-shortcut-groups" aria-label={t('dashboard.profileSettings')}>
                  <section className="dashboard-shortcut-group" aria-labelledby="profile-settings-title">
                    <div className="profile-settings-heading"><h3 id="profile-settings-title">{t('dashboard.profileSettings')}</h3><span className="profile-context">{dashboardSelectedProfileName || t('dashboard.profileContext')}</span></div>
                    <div className="section-shortcuts">
                      <button type="button" onClick={() => editSettings('gaming-display')}><span>{t('dashboard.summaryScreen')}</span><small>{t('dashboard.summaryScreenHint')}</small></button>
                      <button type="button" onClick={() => editSettings('launcher')}><span>{t('dashboard.summaryLauncher')}</span><small>{t('dashboard.summaryLauncherHint')}</small></button>
                      <button type="button" onClick={() => editSettings('console-behaviour')}><span>{t('dashboard.summaryBehaviour')}</span><small>{t('dashboard.summaryBehaviourHint')}</small></button>
                      <button type="button" onClick={() => editSettings('controller')}><span>{t('dashboard.summaryShortcuts')}</span><small>{t('dashboard.summaryShortcutsHint')}</small></button>
                    </div>
                  </section>
                </div>
              </section>
            </div>
          </section>
          <footer className="setup-footer"><p className={hasError ? 'error-line' : 'operation-line'}>{message}</p></footer>
          {showDeleteProfileDialog ? (
            <div className="dialog-backdrop" role="presentation"><div className="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="delete-profile-dialog-title">
              <h3 id="delete-profile-dialog-title">{t('dashboard.deleteDialog')}</h3>
              <p>{t('dashboard.deleteDialogConfirm', { name: selectedProfile ? selectedProfile.name : '' })}</p>
              <div className="dialog-actions">
                <button className="secondary-button" type="button" disabled={isRemovingProfile} onClick={() => setShowDeleteProfileDialog(false)}>{t('dashboard.deleteDialogCancel')}</button>
                <button className="danger-button secondary-button" type="button" disabled={isRemovingProfile} onClick={() => { void (async () => { await deleteSelectedProfile(); setShowDeleteProfileDialog(false); })(); }}>{isRemovingProfile ? t('dashboard.deleteDialogDeleting') : t('dashboard.deleteDialogDelete')}</button>
              </div>
            </div></div>
          ) : null}
        </div>
      </section>
    </main>
  );
}
