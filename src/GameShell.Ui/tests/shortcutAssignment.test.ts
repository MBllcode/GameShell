import assert from 'node:assert/strict';
import test from 'node:test';
import { tryAssignProfileHotkey } from '../src/features/profiles/shortcutAssignment.ts';

test('rejecting a duplicate profile hotkey keeps the previous assignment', () => {
  const profileHotkeys = {
    MAIN: { enterProfile: 'Ctrl+Alt+M' },
    SECONDARY: { enterProfile: 'Ctrl+Alt+S' }
  };

  const result = tryAssignProfileHotkey('MAIN', 'Ctrl+Alt+S', profileHotkeys, 'Ctrl+Alt+W');

  assert.equal(result.accepted, false);
  assert.equal(result.validationResult, 'Conflict');
  assert.deepEqual(result.value, profileHotkeys);
});

test('rejecting a duplicate profile hotkey leaves an unassigned field empty', () => {
  const profileHotkeys = {
    OTHER: { enterProfile: 'Ctrl+Alt+O' }
  };

  const result = tryAssignProfileHotkey('NEW', 'Ctrl+Alt+O', profileHotkeys, 'Ctrl+Alt+W');

  assert.equal(result.accepted, false);
  assert.equal(result.value.NEW, undefined);
});
