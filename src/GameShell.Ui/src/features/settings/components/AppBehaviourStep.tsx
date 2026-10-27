import { useTranslation } from 'react-i18next';
import { setI18nLanguage } from '../../../i18n/i18n';
import { LANGUAGE_LABELS, SUPPORTED_LANGUAGES, type SupportedLanguage } from '../../../i18n/language.ts';
import { SettingRow } from '../../../shared/ui/SettingRow';
import { SettingsSection } from '../../../shared/ui/SettingsSection';
import type { DisplayLayoutProfile } from '../../../bridge/contracts';
import { IconBadge, PageHeader } from '../../../shared/ui/UiPrimitives';

type FinishOptions = {
  startWithWindows: boolean;
  startMinimized: boolean;
  startMenuShortcut: boolean;
  desktopShortcut: boolean;
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
  return (
    <div className="wizard-copy">
      <PageHeader eyebrow={`${t('setup.step')} ${stepNumber}`} title={t('behaviour.appTitle')} description={t('behaviour.appDescription')} />

      {showLanguageSelector ? (
        <div className="language-panel">
          <IconBadge name="globe" size={18} className="panel-leading-icon" />
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
            icon="startup"
            showInfo
            checked={finishOptions.startWithWindows}
            onChange={(value) => setFinishOptions({
              ...finishOptions,
              startWithWindows: value,
              startMinimized: value ? finishOptions.startMinimized : false
            })}
          />
          <SettingRow
            title={t('behaviour.startMinimized')}
            icon="minimize"
            showInfo
            checked={finishOptions.startMinimized}
            disabled={!finishOptions.startWithWindows}
            onChange={(value) => setFinishOptions({ ...finishOptions, startMinimized: value })}
          />
          <div className="startup-target-options">
            <div className="startup-form-row">
              <IconBadge name="startup" size={16} className="startup-form-icon" />
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
                <IconBadge name="profile" size={16} className="startup-form-icon" />
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
            icon="shortcut"
            showInfo
            checked={finishOptions.startMenuShortcut}
            onChange={(value) => setFinishOptions({ ...finishOptions, startMenuShortcut: value })}
          />
          <SettingRow
            title={t('behaviour.createDesktopShortcut')}
            icon="desktop"
            showInfo
            checked={finishOptions.desktopShortcut}
            onChange={(value) => setFinishOptions({ ...finishOptions, desktopShortcut: value })}
          />
        </SettingsSection>
      </div>
    </div>
  );
}
