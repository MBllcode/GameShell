import assert from 'node:assert/strict';
import test from 'node:test';
import { formatCurrentMode } from '../src/utils/status.ts';
import type { AppStatus } from '../src/types/bridge.ts';

const workModeStatus: AppStatus = {
  state: 'WorkMode',
  currentMode: 'Work',
  sessionState: 'Idle',
  isBusy: false,
  displayMode: 'NativeDisplayConfig',
  selectedConsoleDisplay: null,
  hasPcGamingLayout: true,
  activeProfileName: null,
  activeLauncher: null,
  lastOperation: 'Enter Work Mode',
  lastOperationDurationMs: 10,
  lastError: null,
  settingsPath: 'test'
};

test('Work Mode runtime snapshot renders Work Mode instead of gaming mode', () => {
  assert.equal(formatCurrentMode(workModeStatus), 'Work Mode');
  assert.equal(workModeStatus.sessionState, 'Idle');
  assert.equal(workModeStatus.activeProfileName, null);
  assert.equal(workModeStatus.activeLauncher, null);
});
