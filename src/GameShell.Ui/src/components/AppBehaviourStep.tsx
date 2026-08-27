import { useTranslation } from 'react-i18next';
import { setI18nLanguage } from '../i18n/i18n';
import { LANGUAGE_LABELS, SUPPORTED_LANGUAGES, type SupportedLanguage } from '../i18n/language.ts';
import { SettingRow } from './SettingRow';
import { SettingsSection } from './SettingsSection';
import { sendBridgeCommand } from '../services/webViewBridge';
import { useEffect, useState } from 'react';
import type { DisplayLayoutProfile } from '../types/bridge';

type FinishOptions = {
  startWithWindows: boolean;
  startMinimized: boolean;
  startMenuShortcut: boolean;
  desktopShortcut: boolean;
  consoleShellModeEnabled: boolean;
  startupBehavior: 'AlwaysWorkMode' | 'RestoreLastActiveMode' | 'AlwaysProfile';
  startupProfileId: string | null;
};

export function AppBehaviourStep({
  stepNumber,
  finishOptions,
  setFinishOptions,
  language,
  setLanguage,
  showLanguageSelector = true,
  profiles
}: {
  stepNumber: number;
  finishOptions: FinishOptions;
  setFinishOptions: (options: FinishOptions) => void;
  language: SupportedLanguage;
  setLanguage: (value: SupportedLanguage) => void;
  showLanguageSelector?: boolean;
  profiles: DisplayLayoutProfile[];
}) {
  const { t } = useTranslation();
  const [shellSupport, setShellSupport] = useState<{ isSupported: boolean; edition: string; reason: string } | null>(null);

  useEffect(() => {
    void sendBridgeCommand<{ isSupported: boolean; edition: string; reason: string }>('getConsoleShellStatus')
      .then(setShellSupport)
      .catch(() => setShellSupport(null));
  }, []);

  return (
    <div className="wizard-copy">
      <p className="eyebrow">{t('setup.step')} {stepNumber}</p>
      <h2>{t('behaviour.appTitle')}</h2>
      <div className="settings-intro">
        <p>{t('behaviour.appDescription')}</p>
      </div>

      {showLanguageSelector ? (
        <div className="language-panel">
          <label className="field-label" htmlFor="app-language-select">{t('language.selectorLabel')}</label>
          <select
            id="app-language-select"
            className="language-select"
            value={language}
            onChange={(event) => {
              const nextLanguage = event.target.value as SupportedLanguage;
              setLanguage(nextLanguage);
              setI18nLanguage(nextLanguage);
            }}
          >
            {SUPPORTED_LANGUAGES.map((code) => (
              <option key={code} value={code}>
                {LANGUAGE_LABELS[code]}
              </option>
            ))}
          </select>
        </div>
      ) : null}

      <div className="settings-category-list app-behaviour-settings">
        <SettingsSection title={t('behaviour.startupSection')}>
          <SettingRow
            title={t('behaviour.startWithWindows')}
            checked={finishOptions.startWithWindows}
            onChange={(value) => setFinishOptions({
              ...finishOptions,
              startWithWindows: value,
              startMinimized: value ? finishOptions.startMinimized : false
            })}
          />
          <SettingRow
            title={t('behaviour.startMinimized')}
            checked={finishOptions.startMinimized}
            disabled={!finishOptions.startWithWindows}
            onChange={(value) => setFinishOptions({ ...finishOptions, startMinimized: value })}
          />
          <SettingRow
            title={t('behaviour.consoleShellMode')}
            description={t('behaviour.consoleShellModeDescription')}
            checked={finishOptions.consoleShellModeEnabled}
            disabled={!finishOptions.consoleShellModeEnabled && shellSupport?.isSupported === false}
            trailing={shellSupport ? (
              <span className={`console-shell-status ${shellSupport.isSupported ? 'is-available' : 'is-unavailable'}`}>
                {t(shellSupport.isSupported ? 'behaviour.consoleShellAvailable' : 'behaviour.consoleShellUnavailable')}
              </span>
            ) : null}
            helper={!finishOptions.consoleShellModeEnabled && shellSupport?.isSupported === false
              ? t('behaviour.consoleShellUnavailableMessage')
              : null}
            onChange={(value) => setFinishOptions({ ...finishOptions, consoleShellModeEnabled: value })}
          />
          {finishOptions.consoleShellModeEnabled ? (
            <div className="console-shell-help">
              <p>{t('behaviour.consoleShellWarning')}</p>
              <p>{t('behaviour.consoleShellRecovery')}</p>
            </div>
          ) : null}
          <div className="startup-target-options">
            <div className="startup-form-row">
              <label className="field-label" htmlFor="startup-behavior-select">{t('behaviour.startupTarget')}</label>
              <select
                id="startup-behavior-select"
                className="language-select"
                value={finishOptions.startupBehavior}
                onChange={(event) => setFinishOptions({ ...finishOptions, startupBehavior: event.target.value as FinishOptions['startupBehavior'] })}
              >
                <option value="AlwaysWorkMode">{t('behaviour.startupAlwaysWork')}</option>
                <option value="RestoreLastActiveMode">{t('behaviour.startupRestoreLast')}</option>
                <option value="AlwaysProfile">{t('behaviour.startupAlwaysProfile')}</option>
              </select>
            </div>
            {finishOptions.startupBehavior === 'AlwaysProfile' ? (
              <div className="startup-form-row">
                <label className="field-label" htmlFor="startup-profile-select">{t('behaviour.startupProfile')}</label>
                <select
                  id="startup-profile-select"
                  className="language-select"
                  value={finishOptions.startupProfileId ?? ''}
                  onChange={(event) => setFinishOptions({ ...finishOptions, startupProfileId: event.target.value || null })}
                >
                  <option value="">{t('behaviour.selectProfile')}</option>
                  {profiles.map((profile) => {
                    const profileId = profile.id ?? profile.name;
                    return <option key={profileId} value={profileId}>{profile.name}</option>;
                  })}
                </select>
              </div>
            ) : null}
          </div>
        </SettingsSection>
        <SettingsSection title={t('behaviour.shortcutsSection')}>
          <SettingRow
            title={t('behaviour.createStartMenuShortcut')}
            checked={finishOptions.startMenuShortcut}
            onChange={(value) => setFinishOptions({ ...finishOptions, startMenuShortcut: value })}
          />
          <SettingRow
            title={t('behaviour.createDesktopShortcut')}
            checked={finishOptions.desktopShortcut}
            onChange={(value) => setFinishOptions({ ...finishOptions, desktopShortcut: value })}
          />
        </SettingsSection>
      </div>
    </div>
  );
}
