import assert from 'node:assert/strict';
import test from 'node:test';
import { applyEdgeSnap, canvasToTopology, computeTopologyBounds, createCanvasTransform, rectFromPosition, rectsOverlap, resolveCollision, topologyToCanvas } from '../src/utils/displayTopologyGeometry.ts';
import { getProfileTopology, getRefreshPolicy, getRefreshSelectionState, profileTargetFromDisplay, topologyFromLegacy, topologyToLegacy, validateTopology } from '../src/utils/displays.ts';
import type { DisplayDeviceInfo, DisplayIdentity, DisplayLayoutSnapshot, DisplayModeInfo, GamingProfileDisplayTopology, ProfileDisplayTarget } from '../src/types/bridge.ts';

function identity(name: string): DisplayIdentity {
  return { monitorDevicePath: `\\\\?\\DISPLAY#${name}`, friendlyName: name, lastKnownFriendlyName: name, edidManufacturerId: 1, edidProductCodeId: name.length, connectorInstance: 1, adapterLuid: 1, targetId: name.length, sourceId: 1, outputTechnology: 'HDMI' };
}

function topology(count: number): GamingProfileDisplayTopology {
  return { displays: Array.from({ length: count }, (_, index) => ({ identity: identity(`DISPLAY-${index}`), enabled: true, isPrimary: index === 0, positionX: index === 0 ? 0 : -index * 1920, positionY: index * 100, width: 1920, height: 1080, refreshRateNumerator: 60, refreshRateDenominator: 1, orientation: 'Identity' })) };
}

test('supports topologies with more than five displays', () => {
  assert.equal(validateTopology(topology(1)).length, 0);
  assert.equal(validateTopology(topology(2)).length, 0);
  assert.equal(validateTopology(topology(5)).length, 0);
  assert.equal(validateTopology(topology(8)).length, 0);
});

test('disabled display remains a profile target and cannot be primary', () => {
  const value = topology(2);
  value.displays[1].enabled = false;
  value.displays[1].isPrimary = false;
  assert.equal(validateTopology(value).length, 0);
  const legacy = topologyToLegacy(value, []);
  assert.equal(legacy.displays[1].isActive, false);
});

test('legacy migration keeps inactive entries and unknown refresh', () => {
  const legacy: DisplayLayoutSnapshot = { id: 'legacy', capturedAt: new Date(0).toISOString(), displays: [{ identity: identity('S90F'), isActive: false, isAvailable: false, isPrimary: false, positionX: -1920, positionY: 360, width: 1920, height: 1080, orientation: 'Identity', refreshRateNumerator: 0, refreshRateDenominator: 0, refreshRateHz: 0, currentModeAvailable: false, preferredWidth: 0, preferredHeight: 0, sourceId: 0, targetId: 1, adapterLuid: 1, sourceDeviceName: '', connectionTechnology: '' }] };
  const migrated = topologyFromLegacy(legacy);
  assert.equal(migrated.displays.length, 1);
  assert.equal(migrated.displays[0].enabled, false);
  assert.equal(migrated.displays[0].refreshRateDenominator, 0);
});

test('profile topology takes precedence over legacy layout', () => {
  const legacy: DisplayLayoutSnapshot = { id: 'legacy', capturedAt: new Date(0).toISOString(), displays: [] };
  const profile = { name: 'PC', layout: legacy, displayTopology: topology(2) };
  assert.equal(getProfileTopology(profile).displays.length, 2);
});

test('current 4K144 layout preserves rational refresh in the profile topology', () => {
  const legacy: DisplayLayoutSnapshot = { id: 'current', capturedAt: new Date(0).toISOString(), displays: [{ identity: identity('S90F'), isActive: true, isAvailable: true, isPrimary: true, positionX: 0, positionY: 0, width: 3840, height: 2160, orientation: 'Identity', refreshRateNumerator: 144000, refreshRateDenominator: 1000, refreshRateHz: 144, currentModeAvailable: true, preferredWidth: 3840, preferredHeight: 2160, sourceId: 0, targetId: 1, adapterLuid: 1, sourceDeviceName: '\\\\.\\DISPLAY1', connectionTechnology: 'HDMI' }] };
  const target = topologyFromLegacy(legacy).displays[0];
  assert.equal(target.refreshPolicy, 'AutoHighest');
  assert.equal(target.refreshRateNumerator, 0);
});

test('active detected display addition defaults to AutoHighest instead of current refresh', () => {
  const display = { identity: identity('S90F'), name: 'S90F', isPrimary: true, isEnabled: true, isAvailable: true, sourceDeviceName: '\\\\.\\DISPLAY1', sourceId: 0, targetId: 1, adapterLuid: 1, connectionTechnology: 'HDMI', positionX: 0, positionY: 0, width: 3840, height: 2160, orientation: 'Identity', refreshRateNumerator: 144000, refreshRateDenominator: 1000, refreshRateHz: 144, currentModeAvailable: true, preferredWidth: 3840, preferredHeight: 2160 } satisfies DisplayDeviceInfo;
  const target = profileTargetFromDisplay(display);
  assert.equal(target.refreshPolicy, 'AutoHighest');
  assert.equal(target.refreshRateNumerator, 0);
});

test('AutoHighest display with unknown refresh can be saved', () => {
  const value = topology(1);
  value.displays[0].refreshRateNumerator = 0;
  value.displays[0].refreshRateDenominator = 0;
  assert.equal(validateTopology(value).length, 0);
});

test('AutoHighest ignores stale exact refresh fields during hydration', () => {
  const value = topology(1);
  value.displays[0].refreshPolicy = 'AutoHighest';
  value.displays[0].refreshRateNumerator = 60;
  value.displays[0].refreshRateDenominator = 1;
  value.displays[0].fixedRefreshRateNumerator = 60;
  value.displays[0].fixedRefreshRateDenominator = 1;

  assert.equal(getRefreshPolicy(value.displays[0]), 'AutoHighest');
  assert.equal(getRefreshSelectionState(value.displays[0], { isEnabled: false }, []).modes.length, 0);
  assert.equal(validateTopology(value).length, 0);
});

function target(refreshRateNumerator = 144, refreshRateDenominator = 1): ProfileDisplayTarget {
  return { identity: identity('S90F'), enabled: true, isPrimary: true, positionX: 0, positionY: 0, width: 3840, height: 2160, refreshPolicy: 'Fixed', fixedRefreshRateNumerator: refreshRateNumerator, fixedRefreshRateDenominator: refreshRateDenominator, refreshRateNumerator: 0, refreshRateDenominator: 0, orientation: 'Identity' };
}

function mode(refreshRate: number): DisplayModeInfo {
  return { width: 3840, height: 2160, refreshRateNumerator: refreshRate, refreshRateDenominator: 1, refreshRateHz: refreshRate };
}

test('active display uses available refresh modes and selects saved 144', () => {
  const state = getRefreshSelectionState(target(), { isEnabled: true }, [mode(60), mode(120), mode(144)]);
  assert.deepEqual(state.modes.map((value) => value.refreshRateHz), [60, 120, 144]);
  assert.equal(state.disabled, false);
  assert.equal(state.modes.some((value) => value.refreshRateNumerator === 144), true);
});

test('inactive display preserves saved refresh when modes are unavailable', () => {
  const state = getRefreshSelectionState(target(), { isEnabled: false }, []);
  assert.equal(state.modes[0].refreshRateHz, 144);
  assert.equal(state.disabled, true);
  assert.equal(state.unavailable, false);
});

test('inactive display without saved refresh exposes unavailable state', () => {
  const state = getRefreshSelectionState(target(0, 0), { isEnabled: false }, []);
  assert.equal(state.modes.length, 0);
  assert.equal(state.disabled, true);
  assert.equal(state.unavailable, true);
});

test('saved refresh survives switching observed display from active to inactive', () => {
  const saved = target();
  const active = getRefreshSelectionState(saved, { isEnabled: true }, [mode(144)]);
  const inactive = getRefreshSelectionState(saved, { isEnabled: false }, []);
  assert.equal(active.modes[0].refreshRateHz, 144);
  assert.equal(inactive.modes[0].refreshRateHz, 144);
});

test('snaps a display to the right edge', () => {
  const other = rectFromPosition(0, 0, 1920, 1080);
  const snapped = applyEdgeSnap(rectFromPosition(1940, 4, 1280, 720), [other]);
  assert.equal(snapped.left, 1920);
  assert.equal(snapped.top, 0);
  assert.notEqual(snapped.left, 5360);
  assert.notEqual(snapped.left, -5360);
});

test('snaps a display to the left edge', () => {
  const other = rectFromPosition(0, 0, 1920, 1080);
  const snapped = applyEdgeSnap(rectFromPosition(-3460, 6, 3440, 1440), [other]);
  assert.equal(snapped.left, -3440);
  assert.notEqual(snapped.left, -5360);
  assert.equal(snapped.top, 0);
});

test('3440 display placed right of 1920 display gets X=1920', () => {
  const snapped = applyEdgeSnap(rectFromPosition(1940, 0, 3440, 1440), [rectFromPosition(0, 0, 1920, 1080)]);
  assert.equal(snapped.left, 1920);
});

test('1920 display placed left of 3440 display gets X=-1920', () => {
  const snapped = applyEdgeSnap(rectFromPosition(-1940, 0, 1920, 1080), [rectFromPosition(0, 0, 3440, 1440)]);
  assert.equal(snapped.left, -1920);
});

test('snaps top and bottom adjacency', () => {
  const other = rectFromPosition(0, 0, 1920, 1080);
  assert.equal(applyEdgeSnap(rectFromPosition(4, 1100, 1280, 720), [other]).top, 1080);
  assert.equal(applyEdgeSnap(rectFromPosition(4, -740, 1280, 720), [other]).top, -720);
});

test('resolves partial and full overlap to the nearest free edge', () => {
  const other = rectFromPosition(0, 0, 1920, 1080);
  const partial = resolveCollision(rectFromPosition(1800, 100, 1280, 720), [other]);
  const full = resolveCollision(rectFromPosition(200, 200, 1280, 720), [other]);
  assert.equal(rectsOverlap(partial, other), false);
  assert.equal(rectsOverlap(full, other), false);
});

test('touching edges and corners are not overlap', () => {
  const other = rectFromPosition(0, 0, 100, 100);
  assert.equal(rectsOverlap(rectFromPosition(100, 0, 100, 100), other), false);
  assert.equal(rectsOverlap(rectFromPosition(100, 100, 100, 100), other), false);
});

test('preserves negative coordinates through a scaled canvas transform', () => {
  const bounds = computeTopologyBounds([rectFromPosition(-1920, -1080, 1920, 1080), rectFromPosition(0, 0, 3840, 2160)]);
  const transform = createCanvasTransform(bounds, 800, 320);
  const source = { x: -1920, y: -1080 };
  const canvas = topologyToCanvas(source, transform);
  const restored = canvasToTopology(canvas, transform);
  assert.ok(Math.abs(restored.x - source.x) < 0.001);
  assert.ok(Math.abs(restored.y - source.y) < 0.001);
});

test('supports five displays without pair-specific assumptions', () => {
  const displays = Array.from({ length: 5 }, (_, index) => rectFromPosition(index * 400, index % 2 ? 900 : 0, 320, 180));
  const moved = resolveCollision(rectFromPosition(200, 40, 320, 180), displays);
  assert.equal(rectsOverlap(moved, displays[0]), false);
  assert.equal(displays.length, 5);
});
