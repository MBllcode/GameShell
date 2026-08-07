import assert from 'node:assert/strict';
import test from 'node:test';
import {
  ControllerFamily,
  detectControllerFamily,
  getControllerButtonDisplayName,
  mapControllerButtons
} from '../src/controllerDisplayMapper.ts';

test('maps PlayStation neutral buttons to PlayStation labels', () => {
  assert.equal(getControllerButtonDisplayName('South', ControllerFamily.PlayStation), 'Cross');
  assert.equal(getControllerButtonDisplayName('West', ControllerFamily.PlayStation), 'Square');
  assert.equal(getControllerButtonDisplayName('Back', ControllerFamily.PlayStation), 'Create');
});

test('maps Xbox neutral buttons to Xbox labels', () => {
  assert.equal(getControllerButtonDisplayName('South', ControllerFamily.Xbox), 'A');
  assert.equal(getControllerButtonDisplayName('West', ControllerFamily.Xbox), 'X');
  assert.equal(getControllerButtonDisplayName('Back', ControllerFamily.Xbox), 'View');
});

test('maps Nintendo neutral buttons to Nintendo labels', () => {
  assert.equal(getControllerButtonDisplayName('South', ControllerFamily.Nintendo), 'B');
  assert.equal(getControllerButtonDisplayName('East', ControllerFamily.Nintendo), 'A');
});

test('keeps Generic labels neutral and does not mutate saved button data', () => {
  const savedButtons = ['West', 'Back'];
  const mappedButtons = mapControllerButtons(savedButtons, ControllerFamily.Generic);

  assert.deepEqual(mappedButtons, ['West', 'Back']);
  assert.deepEqual(savedButtons, ['West', 'Back']);
});

test('detects controller families conservatively from device names', () => {
  assert.equal(detectControllerFamily('DualSense Wireless Controller'), ControllerFamily.PlayStation);
  assert.equal(detectControllerFamily('Xbox Wireless Controller'), ControllerFamily.Xbox);
  assert.equal(detectControllerFamily('Nintendo Switch Pro Controller'), ControllerFamily.Nintendo);
  assert.equal(detectControllerFamily('Arcade USB Gamepad'), ControllerFamily.Generic);
});