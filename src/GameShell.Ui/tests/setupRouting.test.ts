import assert from 'node:assert/strict';
import test from 'node:test';
import { getSetupSteps } from '../src/features/setup/setupFlow.ts';
import { assertSetupStateInvariants, canContinueWorkingProfile, resolveBootstrapState, resolveDashboardSetupMode, resolveInitialRoute, resolvePersistedCompletion, resolvePostSaveSetupComplete, resolveSaveCompletion, resolveWorkingProfileName } from '../src/features/setup/setupRouting.ts';

test('hydrated existing configuration opens Dashboard', () => {
  assert.equal(resolveInitialRoute(true, true), 'Dashboard');
});

test('fresh configuration opens FirstRun only after hydration', () => {
  assert.equal(resolveInitialRoute(false, false), 'Loading');
  assert.equal(resolveInitialRoute(true, false), 'FirstRun');
});

test('Dashboard add profile enters CreateProfile with exactly four profile steps', () => {
  assert.equal(resolveDashboardSetupMode('create'), 'CreateProfile');
  assert.deepEqual(getSetupSteps('CreateProfile').map((step) => step.id), ['gaming-display', 'launcher', 'profile-behaviour', 'controller']);
});

test('Dashboard profile edit enters EditProfile with exactly four profile steps', () => {
  assert.equal(resolveDashboardSetupMode('edit-profile'), 'EditProfile');
  assert.deepEqual(getSetupSteps('EditProfile').map((step) => step.id), ['gaming-display', 'launcher', 'profile-behaviour', 'controller']);
});

test('profile setup modes do not expose global settings', () => {
  const profileStepIds = getSetupSteps('CreateProfile').concat(getSetupSteps('EditProfile')).map((step) => step.id);
  assert.equal(profileStepIds.includes('work-display'), false);
  assert.equal(profileStepIds.includes('work-behaviour'), false);
  assert.equal(profileStepIds.includes('work-shortcuts'), false);
  assert.equal(profileStepIds.includes('app-behaviour'), false);
});

test('FirstRun exposes all global sections', () => {
  assert.deepEqual(getSetupSteps('FirstRun').map((step) => step.id), ['welcome', 'gaming-display', 'launcher', 'profile-behaviour', 'controller', 'work-display', 'work-behaviour', 'work-shortcuts', 'app-behaviour', 'finish']);
});

test('Dashboard global settings remain separate from profile setup', () => {
  assert.equal(resolveDashboardSetupMode('edit-global'), 'EditGlobal');
  assert.deepEqual(getSetupSteps('EditGlobal').map((step) => step.id), ['work-display', 'work-behaviour', 'work-shortcuts', 'app-behaviour']);
});

test('only FirstRun marks setup complete while profile saves preserve completion', () => {
  assert.equal(resolveSaveCompletion('FirstRun', false), true);
  assert.equal(resolveSaveCompletion('CreateProfile', true), true);
  assert.equal(resolveSaveCompletion('EditProfile', true), true);
  assert.equal(resolveSaveCompletion('EditGlobal', true), true);
});

test('profile save always returns to Dashboard', () => {
  assert.equal(resolvePostSaveSetupComplete('CreateProfile', false), true);
  assert.equal(resolvePostSaveSetupComplete('EditProfile', false), true);
  assert.equal(resolvePostSaveSetupComplete('EditGlobal', false), true);
  assert.equal(resolvePostSaveSetupComplete('FirstRun', false), false);
});

test('completed persisted setup with profiles resolves to inactive Dashboard bootstrap', () => {
  const state = resolveBootstrapState(true, true);
  assert.deepEqual(state, { route: 'Dashboard', setupMode: 'None', editingProfileId: null, isSetupComplete: true, currentStepIndex: 0, profileDraftName: '' });
});

test('incomplete persisted setup resolves to fresh FirstRun bootstrap', () => {
  const state = resolveBootstrapState(true, false);
  assert.deepEqual(state, { route: 'FirstRun', setupMode: 'FirstRun', editingProfileId: null, isSetupComplete: false, currentStepIndex: 0, profileDraftName: '' });
});

test('selected persisted profile does not change completed Dashboard bootstrap', () => {
  assert.equal(resolveBootstrapState(true, true).route, 'Dashboard');
});

test('FirstRun invariant rejects an editing profile', () => {
  assert.throws(() => assertSetupStateInvariants('FirstRun', 'FirstRun', 'PC'));
  assert.doesNotThrow(() => assertSetupStateInvariants('FirstRun', 'FirstRun', null));
});

test('EditProfile and CreateProfile cannot use the FirstRun route', () => {
  assert.throws(() => assertSetupStateInvariants('FirstRun', 'EditProfile', 'PC'));
  assert.throws(() => assertSetupStateInvariants('FirstRun', 'CreateProfile', null));
  assert.doesNotThrow(() => assertSetupStateInvariants('ProfileSetup', 'EditProfile', 'PC'));
  assert.doesNotThrow(() => assertSetupStateInvariants('ProfileSetup', 'CreateProfile', null));
});

test('initial bootstrap before hydration is Loading and inactive', () => {
  assert.deepEqual(resolveBootstrapState(false, false), { route: 'Loading', setupMode: 'None', editingProfileId: null, isSetupComplete: false, currentStepIndex: 0, profileDraftName: '' });
});

test('profile and global saves preserve persisted completion', () => {
  for (const mode of ['CreateProfile', 'EditProfile', 'EditGlobal'] as const) {
    assert.equal(resolvePersistedCompletion(mode, true, false), true);
    assert.equal(resolvePersistedCompletion(mode, false, true), false);
  }
});

test('only FirstRun finalization changes completion from false to true', () => {
  assert.equal(resolvePersistedCompletion('FirstRun', false, false), false);
  assert.equal(resolvePersistedCompletion('FirstRun', false, true), true);
  assert.equal(resolvePersistedCompletion('EditProfile', true, false), true);
});

test('FirstRun uses fresh working profile context without dashboard selection', () => {
  assert.equal(resolveWorkingProfileName('FirstRun', 'PC', 'OLD'), 'PC');
  assert.equal(canContinueWorkingProfile('FirstRun', 'PC', 1), true);
  assert.equal(canContinueWorkingProfile('FirstRun', '', 1), false);
});

test('CreateProfile keeps draft context across launcher, behaviour and shortcuts', () => {
  const workingName = resolveWorkingProfileName('CreateProfile', 'KONSOLA', 'OLD');
  assert.equal(workingName, 'KONSOLA');
  assert.equal(canContinueWorkingProfile('CreateProfile', workingName, 2), true);
});

test('EditProfile uses its copied draft instead of Dashboard selection', () => {
  assert.equal(resolveWorkingProfileName('EditProfile', 'Salon TV', 'OLD'), 'Salon TV');
  assert.equal(resolveWorkingProfileName('None', '', 'OLD'), 'OLD');
});
