import assert from 'node:assert/strict';
import test from 'node:test';
import { areIdentitySetsEqual, deduplicatePhysicalDisplays, formatDisplayResolution, isSamePhysicalDisplay } from '../src/features/displays/displayTopology.ts';
import type { DisplayDeviceInfo, DisplayIdentity } from '../src/bridge/contracts.ts';

function identity(overrides: Partial<DisplayIdentity> = {}): DisplayIdentity {
  return {
    monitorDevicePath: 'PATH-S90F',
    friendlyName: 'S90F',
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
  });

  assert.equal(isSamePhysicalDisplay(saved, current), true);
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

  assert.equal(isSamePhysicalDisplay(saved, current), false);
});

test('physical identity ignores friendly-name changes for the same adapter target', () => {
  assert.equal(isSamePhysicalDisplay(identity({ friendlyName: 'S90F', targetId: 10, monitorDevicePath: 'PATH-S90F' }), identity({ friendlyName: 'SAMSUNG', targetId: 10, monitorDevicePath: 'PATH-SAMSUNG' })), true);
  assert.equal(isSamePhysicalDisplay(identity({ friendlyName: 'S90F', targetId: 10, monitorDevicePath: 'PATH-S90F', edidProductCodeId: 10 }), identity({ friendlyName: 'SAMSUNG', targetId: 11, monitorDevicePath: 'PATH-OTHER', edidProductCodeId: 11 })), false);
});

test('inactive available display renders preferred resolution instead of 0 x 0', () => {
  assert.equal(formatDisplayResolution({ width: 0, height: 0, preferredWidth: 3840, preferredHeight: 2160 }), '3840 × 2160');
});

test('deduplicates canonical and current observations into one logical display', () => {
  const displays = [
    { identity: identity({ friendlyName: 'S90F', monitorDevicePath: 'PATH-S90F', targetId: 10 }), name: 'S90F', isRemembered: true, currentObservation: { currentFriendlyName: 'SAMSUNG' } },
    { identity: identity({ friendlyName: 'SAMSUNG', monitorDevicePath: 'PATH-SAMSUNG', targetId: 10 }), name: 'SAMSUNG', isRemembered: false }
  ] as unknown as DisplayDeviceInfo[];

  const logical = deduplicatePhysicalDisplays(displays);

  assert.equal(logical.length, 1);
  assert.equal(logical[0].name, 'S90F');
  assert.equal(logical[0].currentObservation?.currentFriendlyName, 'SAMSUNG');
});
