import assert from 'node:assert/strict';
import test from 'node:test';
import { areSettingsEquivalent } from '../src/features/settings/settingsComparison.ts';
import type { AppSettings } from '../src/types/bridge.ts';

function createSettings(): AppSettings {
  return {
    display: { consoleDisplay: null, consoleDisplays: [], gamingLayout: null, gamingProfiles: [] },
    launcher: { type: 'Playnite', executablePath: 'playnite.exe', arguments: '', targetDisplayId: null },
    gameShell: { hideDesktopIcons: true, hideTaskbar: true, closeVisibleWindows: true, minimizeRemainingWindows: true, moveCursorToCorner: true, hideCursor: true, startLauncher: true, bringLauncherToForeground: true, completionDelaySeconds: 5 },
    gaming: { hideDesktopIcons: true, hideTaskbar: true, closeVisibleWindows: true, minimizeRemainingWindows: true, moveCursorToCorner: true, hideCursor: true, startLauncher: true, bringLauncherToForeground: true, completionDelaySeconds: 5 },
    workMode: { closeLauncher: true, showTaskbars: true, showDesktopIcons: true, taskbarRestoreDelaySeconds: 3, desktopIconsDelaySeconds: 15, doNotMinimizeWindows: true, completionDelaySeconds: 20 },
    controller: { enabled: false, consoleMode: null, gamingMode: null, workMode: null },
    hotkeys: { consoleMode: '', gamingMode: '', workMode: '' },
    startup: { startWithWindows: true, startMinimizedToTray: false, createStartMenuShortcut: true, createDesktopShortcut: false },
    ui: { closeToTray: true, isFirstRunCompleted: true, language: 'en' }
  };
}

test('ignores window geometry when comparing persisted settings', () => {
  const saved = createSettings();
  const candidate = createSettings();
  candidate.ui.windowLeft = 400;
  candidate.ui.windowTop = 200;
  candidate.ui.windowWidth = 1600;
  candidate.ui.windowHeight = 900;

  assert.equal(areSettingsEquivalent(saved, candidate), true);
});

test('detects a real setting difference and clears it after reverting', () => {
  const saved = createSettings();
  const candidate = createSettings();
  candidate.hotkeys.gamingMode = 'CTRL+ALT+G';

  assert.equal(areSettingsEquivalent(saved, candidate), false);
  candidate.hotkeys.gamingMode = saved.hotkeys.gamingMode;
  assert.equal(areSettingsEquivalent(saved, candidate), true);
});

test('ignores regenerated display layout metadata when comparing settings', () => {
  const saved = createSettings();
  saved.workMode.defaultDisplayLayout = {
    id: 'saved-layout',
    capturedAt: '2026-08-14T08:00:00.000Z',
    displays: []
  };
  const candidate = createSettings();
  candidate.workMode.defaultDisplayLayout = {
    id: 'draft-layout',
    capturedAt: '2026-08-14T08:30:00.000Z',
    displays: []
  };

  assert.equal(areSettingsEquivalent(saved, candidate), true);
});
