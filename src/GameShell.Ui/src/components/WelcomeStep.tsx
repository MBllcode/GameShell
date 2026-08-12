import { useTranslation } from 'react-i18next';
import { setI18nLanguage } from '../i18n/i18n';
import { LANGUAGE_LABELS, SUPPORTED_LANGUAGES, type SupportedLanguage } from '../i18n/language.ts';

export function WelcomeStep({
  stepNumber,
  language,
  setLanguage
}: {
  stepNumber: number;
  language: SupportedLanguage;
  setLanguage: (value: SupportedLanguage) => void;
}) {
  const { t } = useTranslation();

  return (
    <div className="wizard-copy">
      <p className="eyebrow">{t('setup.step')} {stepNumber}</p>
      <h2>{t('setup.welcomeTitle')}</h2>
      <p>{t('setup.welcomeMessage')}</p>

      <div className="settings-group" style={{ marginTop: '1.25rem' }}>
        <label className="field-label" htmlFor="welcome-language-select">{t('language.selectorLabel')}</label>
        <select
          id="welcome-language-select"
          className="text-input"
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
    </div>
  );
}
