import assert from 'node:assert/strict';
import test from 'node:test';
import { areIdentitySetsEqual, formatDisplayResolution, sameIdentity } from '../src/utils/displays.ts';
import type { DisplayIdentity } from '../src/types/bridge.ts';

function identity(overrides: Partial<DisplayIdentity> = {}): DisplayIdentity {
  return {
    monitorDevicePath: 'PATH-S90F',
    friendlyName: 'S90F',
    lastKnownFriendlyName: 'S90F',
    edidManufacturerId: 1,
    edidProductCodeId: 2,
    connectorInstance: 3,
    adapterLuid: 42,
    targetId: 7,
    sourceId: 1,
    outputTechnology: 'HDMI',
    ...overrides
  };
}

test('saved S90F and current SAMSUNG with changed path remain one logical display', () => {
  const saved = identity();
  const current = identity({
    monitorDevicePath: 'PATH-SAMSUNG',
    friendlyName: 'SAMSUNG',
    lastKnownFriendlyName: 'SAMSUNG'
  });

  assert.equal(sameIdentity(saved, current), true);
  assert.equal(areIdentitySetsEqual([saved], [current]), true);
});

test('same friendly name with different EDID and target is a different display', () => {
  const saved = identity({ friendlyName: 'SAMSUNG' });
  const current = identity({
    monitorDevicePath: 'PATH-OTHER',
    friendlyName: 'SAMSUNG',
    edidManufacturerId: 9,
    edidProductCodeId: 9,
    targetId: 8
  });

  assert.equal(sameIdentity(saved, current), false);
});

test('inactive available display renders preferred resolution instead of 0 x 0', () => {
  assert.equal(formatDisplayResolution({ width: 0, height: 0, preferredWidth: 3840, preferredHeight: 2160 }), '3840 × 2160');
});
