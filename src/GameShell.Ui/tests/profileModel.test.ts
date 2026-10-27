import assert from 'node:assert/strict';
import test from 'node:test';
import { createProfile, profileLayout, profilesFromSettings, updateProfile } from '../src/features/profiles/profileModel.ts';
import { buildSettingsPayload } from '../src/features/settings/buildSettingsPayload.ts';

const baseSettings = () => ({
  display: { gamingProfiles: [{ id: 'tv', name: 'TV', layout: { displays: [] } }],
    gamingProfileConfigurations: [{ profileName: 'TV', launcher: { type: 'Steam' }, behaviour: { startLauncher: false }, hotkeys: { enterProfile: 'Ctrl+Alt+T' } }] },
  launcher: { type: 'Playnite' }, gaming: {}, hotkeys: { gamingMode: '' }, controller: { gamingMode: null }
}) as any;

test('legacy settings become a single complete profile without forcing an application', () => {
  const [profile] = profilesFromSettings(baseSettings());
  assert.equal(profile.id, 'tv');
  assert.equal(profile.application, 'None');
  assert.equal(profile.hotkeys.enterProfile, 'Ctrl+Alt+T');
  assert.equal(profile.controllerShortcut, null);
});

test('renaming one profile preserves its complete settings and leaves other profiles unchanged', () => {
  const tv = createProfile({ id: 'tv', name: 'TV', layout: { displays: [] } }, { application: 'Steam Big Picture', hotkeys: { enterProfile: 'Ctrl+Alt+T' } });
  const desk = createProfile({ id: 'desk', name: 'Desk', layout: { displays: [] } });
  const updated = updateProfile([tv, desk], 'tv', { name: 'Football' });
  assert.equal(updated[0].application, 'Steam Big Picture');
  assert.equal(updated[0].hotkeys.enterProfile, 'Ctrl+Alt+T');
  assert.equal(updated[1], desk);
  assert.equal(tv.name, 'TV');
  assert.deepEqual(Object.keys(profileLayout(updated[0])).sort(), ['displayTopology', 'id', 'layout', 'name']);
});

test('new profiles have independent default behaviour objects', () => {
  const a = createProfile({ name: 'A', layout: { displays: [] } });
  const b = createProfile({ name: 'B', layout: { displays: [] } });
  a.behaviour.hideTaskbar = true;
  assert.equal(b.behaviour.hideTaskbar, false);
  assert.equal(b.application, 'None');
});

test('pending layout saves preserve application and shortcut changes made in later wizard steps', () => {
  const original = createProfile({ id: 'tv', name: 'TV', layout: { displays: [] } });
  const edited = { ...original, application: 'Steam Big Picture' as const, hotkeys: { enterProfile: 'Ctrl+Alt+T' } };
  const state = { settings: null, gamingProfiles: [edited], activeGamingProfileName: 'TV', gamingLayout: null,
    selectedConsoleDisplay: null, selectedConsoleDisplays: [], gamingBehaviour: edited.behaviour, launcher: 'None',
    hotkeys: { gamingMode: '', workMode: '', consoleMode: '' }, consoleBehaviour: edited.behaviour,
    workBehaviour: {}, workModeLayout: null, finishOptions: {}, language: 'pl', workModeControllerShortcut: null } as any;
  const payload = buildSettingsPayload(state, true, { gamingProfiles: [{ ...original, name: 'Football' }] });
  assert.equal(payload.display.gamingProfiles[0].name, 'Football');
  assert.equal(payload.display.gamingProfileConfigurations![0].launcher.type, 'Steam');
  assert.equal(payload.display.gamingProfileConfigurations![0].hotkeys.enterProfile, 'Ctrl+Alt+T');
  assert.equal('application' in payload.display.gamingProfiles[0], false);
});

test('rename/save round trips unexposed configuration and custom application paths', () => {
  const settings = baseSettings();
  settings.gameShell = {}; settings.workMode = {};
  settings.remoteControl = { enabled: true, authenticationToken: 'isolated-test-token' };
  settings.ui = { windowLeft: 30, windowWidth: 1280 };
  settings.display.gamingProfileConfigurations[0].launcher = { type: 'Steam', executablePath: 'D:/Apps/Steam.exe', arguments: '-bigpicture' };
  settings.display.gamingProfileConfigurations[0].behaviour = { startLauncher: true, gameProcessNames: ['example-game'], protectedProcessNames: ['keep-open'] };
  const [original] = profilesFromSettings(settings);
  const renamed = { ...original, name: 'Living room' };
  const state = { settings, gamingProfiles: [renamed], activeGamingProfileName: 'Living room', gamingLayout: null,
    selectedConsoleDisplay: null, selectedConsoleDisplays: [], gamingBehaviour: renamed.behaviour, launcher: 'None',
    hotkeys: { gamingMode: '', workMode: '', consoleMode: '' }, consoleBehaviour: renamed.behaviour,
    workBehaviour: {}, workModeLayout: null, finishOptions: {}, language: 'pl', workModeControllerShortcut: null } as any;
  const saved = buildSettingsPayload(state, true) as any;
  const configuration = saved.display.gamingProfileConfigurations[0];
  assert.equal(configuration.profileName, 'Living room');
  assert.equal(configuration.launcher.executablePath, 'D:/Apps/Steam.exe');
  assert.deepEqual(configuration.behaviour.gameProcessNames, ['example-game']);
  assert.deepEqual(configuration.behaviour.protectedProcessNames, ['keep-open']);
  assert.deepEqual(saved.remoteControl, settings.remoteControl);
  assert.equal(saved.ui.windowWidth, 1280);
});

