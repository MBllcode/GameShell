export const SUPPORTED_LANGUAGES = ['en', 'pl'] as const;
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number];

export const DEFAULT_LANGUAGE: SupportedLanguage = 'en';

export const LANGUAGE_LABELS: Record<SupportedLanguage, string> = {
  en: 'English',
  pl: 'Polski'
};

export function resolveSupportedLanguage(value?: string | null): SupportedLanguage | null {
  const rawCode = (value ?? '').trim().toLowerCase();
  if (!rawCode) {
    return null;
  }

  if (rawCode === 'pl' || rawCode.startsWith('pl-') || rawCode.startsWith('pl_')) {
    return 'pl';
  }

  if (rawCode === 'en' || rawCode.startsWith('en-') || rawCode.startsWith('en_')) {
    return 'en';
  }

  return null;
}

export function normalizeLanguageCode(value?: string | null): SupportedLanguage {
  return resolveSupportedLanguage(value) ?? DEFAULT_LANGUAGE;
}

export function detectPreferredLanguage(candidates: Array<string | null | undefined> = []) {
  const locales = candidates.length > 0
    ? candidates
    : [
        typeof navigator === 'undefined' ? undefined : navigator.language,
        ...(typeof navigator === 'undefined' ? [] : navigator.languages ?? [])
      ];

  for (const candidate of locales) {
    const resolved = resolveSupportedLanguage(candidate);
    if (resolved) {
      return resolved;
    }
  }

  return DEFAULT_LANGUAGE;
}
