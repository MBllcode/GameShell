import assert from 'node:assert/strict';
import fs from 'node:fs';
import test from 'node:test';
import { FIRST_RUN_STEPS } from '../src/features/setup/setupFlow.ts';
import { getI18n, setI18nLanguage } from '../src/i18n/i18n.ts';
import { DEFAULT_LANGUAGE, detectPreferredLanguage, normalizeLanguageCode } from '../src/i18n/language.ts';
import { summarizeLayout } from '../src/utils/displays.ts';
import { formatCurrentMode, formatSessionState } from '../src/utils/status.ts';
import { scheduleValidationErrorClear, triggerValidationError } from '../src/utils/validation.ts';
import { isDuplicateProfileEnterHotkey, isDuplicateWorkModeHotkey } from '../src/features/profiles/utils.ts';
import { isDuplicateProfileControllerShortcut, isDuplicateWorkModeControllerShortcut } from '../src/utils/controllers.ts';
import { tryAssignProfileControllerShortcut, tryAssignProfileHotkey, tryAssignWorkModeControllerShortcut, tryAssignWorkModeHotkey } from '../src/features/profiles/shortcutAssignment.ts';

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

test('does not treat the current profile keyboard shortcut as its own conflict', () => {
  const profileHotkeys = { '  Gaming ': { enterProfile: 'Ctrl+Alt+K' }, Other: { enterProfile: '' } };

  assert.equal(isDuplicateProfileEnterHotkey('Gaming', 'Ctrl+Alt+K', profileHotkeys), false);
  assert.equal(isDuplicateProfileEnterHotkey('Other', 'Ctrl+Alt+K', profileHotkeys), true);
  assert.equal(isDuplicateWorkModeHotkey('Ctrl+Alt+K', profileHotkeys), true);
});

test('does not treat the current profile controller shortcut as its own conflict', () => {
  const profileShortcuts = {
    '  Gaming ': { buttons: ['Square', 'Share'], holdDurationMs: 700 },
    Other: null
  };

  assert.equal(isDuplicateProfileControllerShortcut('Gaming', ['Share', 'Square'], profileShortcuts), false);
  assert.equal(isDuplicateProfileControllerShortcut('Other', ['Square', 'Share'], profileShortcuts), true);
  assert.equal(isDuplicateWorkModeControllerShortcut(['Square', 'Share'], profileShortcuts), true);
});

test('invalid keyboard candidate never replaces the saved profile shortcut across re-entry', () => {
  const saved = { Gaming: { enterProfile: 'Ctrl+Alt+K' }, Other: { enterProfile: 'Ctrl+Alt+S' } };
  const assignment = tryAssignProfileHotkey('Gaming', 'Ctrl+Alt+S', saved, '');

  assert.equal(assignment.accepted, false);
  assert.equal(assignment.validationResult, 'Conflict');
  assert.equal(assignment.value, saved);
  assert.equal(saved.Gaming.enterProfile, 'Ctrl+Alt+K');

  const reentered = saved.Gaming.enterProfile;
  assert.equal(reentered, 'Ctrl+Alt+K');
});

test('invalid controller candidate never replaces the saved profile shortcut across re-entry', () => {
  const saved = {
    Gaming: { buttons: ['Square', 'Share'], holdDurationMs: 700 },
    Other: { buttons: ['Cross', 'Options'], holdDurationMs: 700 }
  };
  const assignment = tryAssignProfileControllerShortcut('Gaming', ['Cross', 'Options'], saved, null);

  assert.equal(assignment.accepted, false);
  assert.equal(assignment.validationResult, 'Conflict');
  assert.equal(assignment.value, saved);
  assert.deepEqual(saved.Gaming.buttons, ['Square', 'Share']);

  const reentered = saved.Gaming.buttons;
  assert.deepEqual(reentered, ['Square', 'Share']);
});

test('valid keyboard and controller candidates update drafts while conflicts do not', () => {
  const hotkeys = { Gaming: { enterProfile: 'Ctrl+Alt+K' }, Other: { enterProfile: 'Ctrl+Alt+S' } };
  const validHotkey = tryAssignProfileHotkey('Gaming', 'Ctrl+Alt+P', hotkeys, '');
  const invalidWorkHotkey = tryAssignWorkModeHotkey('Ctrl+Alt+S', hotkeys);
  assert.equal(validHotkey.accepted, true);
  assert.equal(validHotkey.value.Gaming.enterProfile, 'Ctrl+Alt+P');
  assert.equal(invalidWorkHotkey.accepted, false);
  assert.equal(invalidWorkHotkey.value, 'Ctrl+Alt+S');

  const shortcuts = {
    Gaming: { buttons: ['Square', 'Share'], holdDurationMs: 700 },
    Other: { buttons: ['Cross', 'Options'], holdDurationMs: 700 }
  };
  const validController = tryAssignProfileControllerShortcut('Gaming', ['Triangle', 'L1'], shortcuts, null);
  const invalidWorkController = tryAssignWorkModeControllerShortcut(['Cross', 'Options'], shortcuts, null);
  assert.equal(validController.accepted, true);
  assert.deepEqual(validController.value.Gaming?.buttons, ['Triangle', 'L1']);
  assert.equal(invalidWorkController.accepted, false);
  assert.equal(invalidWorkController.value, null);
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

test('dashboard footer strings use Polish translations instead of falling back to English', () => {
  const i18n = getI18n();

  setI18nLanguage('pl');
  assert.equal(i18n.t('dashboard.editSection'), 'Edytujesz wybraną sekcję.');
  assert.equal(i18n.t('dashboard.createProfileHint'), 'Utwórz nowy profil i przejdź przez kroki konfiguracji.');
  assert.equal(i18n.t('dashboard.switchingTo', { mode: 'Work Mode' }), 'Przełączanie do Work Mode...');

  setI18nLanguage('en');
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

test('controller validation error dismisses automatically after a short delay', async () => {
  const updates: Array<string | null> = [];
  const clear = scheduleValidationErrorClear('Ta kombinacja przycisków jest już przypisana do innego profilu lub Work Mode. Wybierz inną kombinację.', (value) => {
    updates.push(value);
  }, 25);

  assert.deepEqual(updates, ['Ta kombinacja przycisków jest już przypisana do innego profilu lub Work Mode. Wybierz inną kombinację.']);

  await new Promise((resolve) => setTimeout(resolve, 60));
  assert.deepEqual(updates, [
    'Ta kombinacja przycisków jest już przypisana do innego profilu lub Work Mode. Wybierz inną kombinację.',
    null
  ]);

  clear();
});

test('the same validation error can reappear after it has been cleared', async () => {
  const updates: Array<string | null> = [];
  const message = 'Ta kombinacja przycisków jest już przypisana do innego profilu lub Work Mode. Wybierz inną kombinację.';

  triggerValidationError(message, (value) => {
    updates.push(value);
  });

  await new Promise((resolve) => setTimeout(resolve, 0));
  triggerValidationError(message, (value) => {
    updates.push(value);
  });

  await new Promise((resolve) => setTimeout(resolve, 0));

  assert.deepEqual(updates, [
    null,
    message,
    null,
    message
  ]);
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
