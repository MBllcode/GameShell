import test from 'node:test';
import assert from 'node:assert/strict';
import { findApplicationDisplay } from '../src/features/profiles/applicationDisplays.ts';

test('application assignments accept both canonical and legacy display identities', () => {
  const monitor = { knownPhysicalDisplayId: 'physical-1', identity: { monitorDevicePath: 'DISPLAY-A' } } as any;
  assert.equal(findApplicationDisplay('display-a', [monitor]), monitor);
  assert.equal(findApplicationDisplay('PHYSICAL-1', [monitor]), monitor);
  assert.equal(findApplicationDisplay('missing', [monitor]), undefined);
  assert.equal(findApplicationDisplay('', [monitor]), undefined);
});
