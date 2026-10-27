import { createProfile } from '../src/features/profiles/profileModel.ts';
import assert from 'node:assert/strict';
import test from 'node:test';
import { profilesFromSettings } from '../src/features/profiles/profileModel.ts';
import { buildSettingsPayload, type BuildSettingsPayloadState } from '../src/features/settings/buildSettingsPayload.ts';
import { getLauncherSettings, toLauncherType } from '../src/features/profiles/profileCommands.ts';
import { formatSessionState } from '../src/app/status.ts';
import { getSetupSteps } from '../src/features/setup/setupFlow.ts';
import { getResourceBundle } from '../src/i18n/resources.ts';

test('no application is explicit and never maps back to Playnite', () => {
  assert.deepEqual(getLauncherSettings('None'), { type: 'None', executablePath: '', arguments: '' });
  assert.equal(toLauncherType('None'), 'None');
  assert.equal(toLauncherType('Steam Big Picture'), 'Steam Big Picture');
});

test('legacy disabled launching hydrates as None without changing another profile', () => {
  const profiles = [{ name: 'Football' }, { name: 'PC' }] as any;
  const configurations = [
    { profileName: 'Football', launcher: { type: 'Playnite' }, behaviour: { startLauncher: false } },
    { profileName: 'PC', launcher: { type: 'Steam' }, behaviour: { startLauncher: true } }
  ] as any;
  const loaded = profilesFromSettings({ display: { gamingProfiles: profiles, gamingProfileConfigurations: configurations }, launcher: { type: 'Playnite' }, gaming: {}, controller: {}, hotkeys: {} } as any);
  assert.deepEqual(loaded.map(profile => profile.application), ['None', 'Steam Big Picture']);
});

test('saved mixed profiles have independent applications and coherent launch flags', () => {
  const behaviour = { hideDesktopIcons: false, hideTaskbar: false, closeVisibleWindows: false, minimizeRemainingWindows: false, moveCursorToCorner: false, hideCursor: false, startLauncher: true, bringLauncherToForeground: true };
  const state = {
    settings: null, selectedConsoleDisplay: null, selectedConsoleDisplays: [], gamingLayout: null,
    gamingProfiles: ['Football', 'Console', 'PC'].map((name, index) => createProfile({ name, layout: { displays: [] } }, { behaviour, application: ['None', 'Playnite Fullscreen', 'Steam Big Picture'][index] as any })),
    activeGamingProfileName: 'Football', gamingProfileBehaviours: { Football: behaviour, Console: behaviour, PC: behaviour },
    gamingProfileLaunchers: { Football: 'None', Console: 'Playnite Fullscreen', PC: 'Steam Big Picture' },
    gamingProfileHotkeys: {}, gamingProfileControllerShortcuts: {}, workModeControllerShortcut: null, workModeLayout: null,
    gamingBehaviour: behaviour, consoleBehaviour: behaviour, launcher: 'None', language: 'en', hotkeys: { consoleMode: '', gamingMode: '', workMode: '' },
    workBehaviour: {}, finishOptions: { startupBehavior: 'AlwaysWorkMode' }
  } as unknown as BuildSettingsPayloadState;
  const payload = buildSettingsPayload(state, true);
  const configs = payload.display.gamingProfileConfigurations!;
  assert.deepEqual(configs.map(c => c.launcher.type), ['None', 'Playnite', 'Steam']);
  assert.deepEqual(configs.map(c => c.behaviour.startLauncher), [false, true, true]);
  assert.deepEqual(configs.map(c => c.behaviour.bringLauncherToForeground), [false, true, true]);
  assert.equal(behaviour.startLauncher, true, 'building a payload must not mutate shared drafts');
});

test('first run establishes a baseline before offering any profile', () => {
  const ids = getSetupSteps('FirstRun').map(step => step.id);
  assert.ok(ids.indexOf('work-display') < ids.indexOf('gaming-display'));
  assert.ok(ids.indexOf('work-shortcuts') < ids.indexOf('gaming-display'));
  assert.equal(formatSessionState('ProfileActive'), 'profile active');
  for (const locale of ['en', 'pl'] as const) {
    const copy = getResourceBundle(locale);
    assert.ok(copy['setup.skipProfile']);
    assert.ok(copy['launcher.none']);
    assert.ok(copy['launcher.noApplication']);
  }
});
