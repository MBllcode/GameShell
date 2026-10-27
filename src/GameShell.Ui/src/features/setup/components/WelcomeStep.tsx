import { useTranslation } from 'react-i18next';
import { setI18nLanguage } from '../../../i18n/i18n';
import { LANGUAGE_LABELS, SUPPORTED_LANGUAGES, type SupportedLanguage } from '../../../i18n/language.ts';
import { IconBadge, PageHeader } from '../../../shared/ui/UiPrimitives';

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
      <PageHeader eyebrow={`${t('setup.step')} ${stepNumber}`} title={t('setup.welcomeTitle')} description={t('setup.welcomeMessage')} />

      <div className="language-panel">
        <IconBadge name="globe" size={18} className="panel-leading-icon" />
        <label className="field-label" htmlFor="welcome-language-select">{t('language.selectorLabel')}</label>
        <select
          id="welcome-language-select"
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
        <p className="language-panel-hint">{t('language.changeLater')}</p>
      </div>
    </div>
  );
}
