import assert from 'node:assert/strict';
import test from 'node:test';
import { formatCurrentMode, formatTransientOperation } from '../src/utils/status.ts';
import type { AppStatus } from '../src/types/bridge.ts';

const workModeStatus: AppStatus = {
  state: 'WorkMode',
  currentMode: 'Work',
  sessionState: 'Idle',
  isBusy: false,
  displayMode: 'NativeDisplayConfig',
  selectedConsoleDisplay: null,
  hasGamingProfiles: true,
  activeProfileName: null,
  activeLauncher: null,
  lastOperation: 'Enter Work Mode',
  lastOperationDurationMs: 10,
  lastError: null,
  transientOperation: {
    kind: 'None',
    phase: 'None',
    isActive: false,
    messageKey: null,
    startedAt: null,
    transitionId: null
  },
  settingsPath: 'test'
};

test('Work Mode runtime snapshot renders Work Mode instead of gaming mode', () => {
  assert.equal(formatCurrentMode(workModeStatus), 'Work Mode');
  assert.equal(workModeStatus.sessionState, 'Idle');
  assert.equal(workModeStatus.activeProfileName, null);
  assert.equal(workModeStatus.activeLauncher, null);
});

test('completed Work Mode status has no transient operation message', () => {
  assert.equal(formatTransientOperation(workModeStatus.transientOperation), '');
});

test('transient operation phase maps to localized status text', () => {
  const operation = {
    kind: 'ExitToWorkMode' as const,
    phase: 'RestoringDisplays' as const,
    isActive: true,
    messageKey: 'status.transient.restoringDisplays',
    startedAt: null,
    transitionId: 'work-1'
  };

  assert.equal(formatTransientOperation(operation, (key) => ({
    'status.transient.restoringDisplays': 'Przywracanie ekranów...'
  }[key] ?? key)), 'Przywracanie ekranów...');
});
