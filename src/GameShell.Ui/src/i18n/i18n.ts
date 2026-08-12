import i18next from 'i18next';
import { initReactI18next } from 'react-i18next';
import { DEFAULT_LANGUAGE, detectPreferredLanguage, type SupportedLanguage, normalizeLanguageCode } from './language.ts';
import { getResourceBundle } from './resources.ts';

const i18n = i18next.createInstance();

const resources = {
  en: { translation: getResourceBundle('en') },
  pl: { translation: getResourceBundle('pl') }
};

const detectedLanguage = detectPreferredLanguage();

void i18n
  .use(initReactI18next)
  .init({
    resources,
    lng: detectedLanguage,
    fallbackLng: DEFAULT_LANGUAGE,
    supportedLngs: ['en', 'pl'],
    interpolation: { escapeValue: false },
    react: { useSuspense: false }
  });

export function setI18nLanguage(language?: string | null) {
  const requestedLanguage = language ?? null;
  const previousLanguage = getCurrentLanguage();
  const nextLanguage = normalizeLanguageCode(language);
  console.info('[I18N LANGUAGE CHANGE]', {
    requested: requestedLanguage,
    previous: previousLanguage,
    current: i18n.resolvedLanguage ?? previousLanguage,
    resolvedLanguage: nextLanguage
  });

  void i18n.changeLanguage(nextLanguage).then(() => {
    console.info('[I18N LANGUAGE CHANGE]', {
      requested: requestedLanguage,
      previous: previousLanguage,
      current: getCurrentLanguage(),
      resolvedLanguage: nextLanguage
    });
  });

  return nextLanguage;
}

export function getCurrentLanguage(): SupportedLanguage {
  return normalizeLanguageCode(i18n.language || undefined);
}

export function getI18n() {
  return i18n;
}

export default i18n;
