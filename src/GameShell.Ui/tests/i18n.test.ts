import assert from 'node:assert/strict';
import fs from 'node:fs';
import test from 'node:test';
import { FIRST_RUN_STEPS } from '../src/features/setup/setupFlow.ts';
import { getI18n, setI18nLanguage } from '../src/i18n/i18n.ts';
import { DEFAULT_LANGUAGE, detectPreferredLanguage, normalizeLanguageCode } from '../src/i18n/language.ts';
import { summarizeLayout } from '../src/utils/displays.ts';
import { formatCurrentMode, formatSessionState } from '../src/utils/status.ts';

test('normalizes supported locales to the canonical language codes', () => {
  assert.equal(normalizeLanguageCode('pl-PL'), 'pl');
  assert.equal(normalizeLanguageCode('en-US'), 'en');
  assert.equal(normalizeLanguageCode('fr-FR'), 'en');
  assert.equal(normalizeLanguageCode(''), DEFAULT_LANGUAGE);
  assert.equal(normalizeLanguageCode(undefined), DEFAULT_LANGUAGE);
});

test('detects Polish when a Polish system locale is available', () => {
  assert.equal(detectPreferredLanguage(['pl-PL', 'en-US']), 'pl');
  assert.equal(detectPreferredLanguage(['fr-FR', 'pl']), 'pl');
  assert.equal(detectPreferredLanguage(['es-MX', 'pt-BR']), 'en');
});

test('first-run steps are defined through translation keys instead of hardcoded Polish strings', () => {
  assert.ok(FIRST_RUN_STEPS.length > 0);
  assert.ok(FIRST_RUN_STEPS.every((step) => typeof step.titleKey === 'string' && typeof step.subtitleKey === 'string'));
  assert.ok(!FIRST_RUN_STEPS.some((step) => step.title === 'Start' || step.title === 'Pierwsza konfiguracja'));
});

test('runtime language change updates the active i18n instance immediately', () => {
  const i18n = getI18n();

  setI18nLanguage('pl');
  assert.equal(i18n.t('setup.welcomeTitle'), 'Konfiguracja GameShell');
  assert.equal(i18n.t('setup.back'), 'Wróć');
  assert.equal(i18n.t('setup.next'), 'Dalej');

  setI18nLanguage('en');
  assert.equal(i18n.t('setup.welcomeTitle'), 'GameShell setup');
  assert.equal(i18n.t('setup.back'), 'Back');
  assert.equal(i18n.t('setup.next'), 'Next');
});

test('runtime status and layout summaries stay localized with the active language', () => {
  const translations: Record<string, string> = {
    'status.gamingMode': 'Tryb gamingowy',
    'status.workMode': 'Work Mode',
    'status.session.launchingProfile': 'uruchamianie profilu',
    'profile.summaryNoActiveDisplays': 'Brak aktywnych ekranów',
    'profile.displayUnknown': 'Nieznany ekran'
  };
  const t = (key: string) => translations[key] ?? key;

  assert.equal(formatCurrentMode({ currentMode: 'Console', state: 'GameShell' } as any, t), 'Tryb gamingowy');
  assert.equal(formatSessionState('EnteringProfile', t), 'uruchamianie profilu');
  assert.equal(summarizeLayout({ displays: [{ isActive: false }] } as any, t), 'Brak aktywnych ekranów');
  assert.equal(summarizeLayout({ displays: [{ isActive: true, identity: { friendlyName: '' }, width: 1920, height: 1080 }] } as any, t), 'Nieznany ekran 1920 × 1080');
});

test('first-run components rely on translation keys rather than hardcoded Polish literals', () => {
  const firstRunFiles = [
    '../src/components/WelcomeStep.tsx',
    '../src/components/AppBehaviourStep.tsx',
    '../src/components/DisplaysStep.tsx',
    '../src/components/LauncherStep.tsx',
    '../src/components/ConsoleBehaviourStep.tsx',
    '../src/components/WorkBehaviourStep.tsx',
    '../src/components/ControllerStep.tsx',
    '../src/components/FinishStep.tsx',
    '../src/pages/ProfileSetupPage.tsx'
  ];

  for (const filePath of firstRunFiles) {
    const source = fs.readFileSync(new URL(filePath, import.meta.url), 'utf8');

    assert.doesNotMatch(source, /Konfiguracja GameShell|Wróć|Dalej|Pierwsza konfiguracja|Ekrany|Launcher profilu|Zachowanie profilu|Zachowanie aplikacji|Podsumowanie ustawień|Język interfejsu|Aktywny profil|Brak zaznaczonego profilu|Gotowe\.|ZAPISANO|Uruchom launcher/);
  }
});
