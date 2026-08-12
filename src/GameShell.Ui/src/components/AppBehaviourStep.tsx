import { useTranslation } from 'react-i18next';
import { setI18nLanguage } from '../i18n/i18n';
import { LANGUAGE_LABELS, SUPPORTED_LANGUAGES, type SupportedLanguage } from '../i18n/language.ts';
import { SettingRow } from './SettingRow';

type FinishOptions = {
  startWithWindows: boolean;
  startMinimized: boolean;
  startMenuShortcut: boolean;
  desktopShortcut: boolean;
};

export function AppBehaviourStep({
  stepNumber,
  finishOptions,
  setFinishOptions,
  language,
  setLanguage,
  showLanguageSelector = true
}: {
  stepNumber: number;
  finishOptions: FinishOptions;
  setFinishOptions: (options: FinishOptions) => void;
  language: SupportedLanguage;
  setLanguage: (value: SupportedLanguage) => void;
  showLanguageSelector?: boolean;
}) {
  const { t } = useTranslation();

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

      <div className="settings-group">
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
          title={t('behaviour.createStartMenuShortcut')}
          checked={finishOptions.startMenuShortcut}
          onChange={(value) => setFinishOptions({ ...finishOptions, startMenuShortcut: value })}
        />
        <SettingRow
          title={t('behaviour.createDesktopShortcut')}
          checked={finishOptions.desktopShortcut}
          onChange={(value) => setFinishOptions({ ...finishOptions, desktopShortcut: value })}
        />
      </div>
    </div>
  );
}
