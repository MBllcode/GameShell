import type { DisplayDeviceInfo, DisplayIdentity, DisplayLayoutProfile, DisplayLayoutSnapshot, DisplayModeInfo, GamingProfileDisplayTopology, ProfileDisplayTarget, RefreshPolicy } from '../types/bridge';
import { placeRectNextToTopology, rectFromPosition, rectsOverlap, type GeometryRect } from './displayTopologyGeometry.ts';

export function hasValidProfileRefresh(target: Pick<ProfileDisplayTarget, 'refreshRateNumerator' | 'refreshRateDenominator'>) {
  return target.refreshRateNumerator > 0
    && target.refreshRateDenominator > 0
    && Number.isFinite(target.refreshRateNumerator / target.refreshRateDenominator);
}

export function getRefreshPolicy(target: Pick<ProfileDisplayTarget, 'refreshPolicy'>): RefreshPolicy {
  return target.refreshPolicy ?? 'AutoHighest';
}

export function hasValidFixedRefresh(target: Pick<ProfileDisplayTarget, 'fixedRefreshRateNumerator' | 'fixedRefreshRateDenominator'>) {
  return (target.fixedRefreshRateNumerator ?? 0) > 0
    && (target.fixedRefreshRateDenominator ?? 0) > 0
    && Number.isFinite((target.fixedRefreshRateNumerator ?? 0) / (target.fixedRefreshRateDenominator ?? 1));
}

export function getRefreshSelectionState(
  target: Pick<ProfileDisplayTarget, 'width' | 'height' | 'refreshRateNumerator' | 'refreshRateDenominator' | 'refreshPolicy' | 'fixedRefreshRateNumerator' | 'fixedRefreshRateDenominator'>,
  observed: Pick<DisplayDeviceInfo, 'isEnabled'> | null,
  availableModes: DisplayModeInfo[]
) {
  if (observed?.isEnabled && availableModes.length > 0) {
    return { modes: availableModes, disabled: false, unavailable: false };
  }

  if (observed?.isEnabled) {
    return { modes: [], disabled: true, unavailable: false };
  }

  if (getRefreshPolicy(target) === 'Fixed' && hasValidFixedRefresh(target)) {
    return {
      modes: [{ width: target.width, height: target.height, refreshRateNumerator: target.fixedRefreshRateNumerator!, refreshRateDenominator: target.fixedRefreshRateDenominator!, refreshRateHz: target.fixedRefreshRateNumerator! / target.fixedRefreshRateDenominator! }],
      disabled: true,
      unavailable: false
    };
  }

  return { modes: [], disabled: true, unavailable: Boolean(observed && !observed.isEnabled) };
}

export function topologyFromLegacy(layout: DisplayLayoutSnapshot): GamingProfileDisplayTopology {
  return {
    displays: layout.displays.map((display) => ({
      identity: display.identity,
      enabled: display.isActive,
      isPrimary: display.isPrimary,
      positionX: display.positionX,
      positionY: display.positionY,
      width: display.width,
      height: display.height,
      refreshPolicy: 'AutoHighest',
      fixedRefreshRateNumerator: 0,
      fixedRefreshRateDenominator: 0,
      refreshRateNumerator: 0,
      refreshRateDenominator: 0,
      orientation: display.orientation
    }))
  };
}

export function normalizeCanvasTopology(topology: GamingProfileDisplayTopology): GamingProfileDisplayTopology {
  const placedRects: GeometryRect[] = [];
  let changed = false;
  const displays = topology.displays.map(display => {
    const desired = rectFromPosition(display.positionX, display.positionY, display.width, display.height);
    const resolved = placedRects.some(rect => rectsOverlap(desired, rect))
      ? placeRectNextToTopology(desired, placedRects)
      : desired;
    placedRects.push(resolved);
    const positionChanged = resolved.left !== desired.left || resolved.top !== desired.top;
    changed ||= positionChanged;
    return positionChanged
      ? { ...display, positionX: Math.round(resolved.left), positionY: Math.round(resolved.top) }
      : display;
  });

  return changed ? { displays } : topology;
}

export function profileTargetFromDisplay(display: DisplayDeviceInfo, fallbackModes: DisplayModeInfo[] = []): ProfileDisplayTarget {
  const mode = fallbackModes.find(candidate => candidate.width === (display.width || display.preferredWidth) && candidate.height === (display.height || display.preferredHeight)) ?? fallbackModes[0];
  return {
    identity: display.identity,
    enabled: true,
    isPrimary: true,
    positionX: display.positionX,
    positionY: display.positionY,
    width: mode?.width ?? display.width ?? display.preferredWidth,
    height: mode?.height ?? display.height ?? display.preferredHeight,
    refreshPolicy: 'AutoHighest',
    fixedRefreshRateNumerator: 0,
    fixedRefreshRateDenominator: 0,
    refreshRateNumerator: 0,
    refreshRateDenominator: 0,
    orientation: display.orientation
  };
}

export function profileTargetForAddition(display: DisplayDeviceInfo, fallbackModes: DisplayModeInfo[] = [], isFirstDisplay: boolean, existingTargets: ProfileDisplayTarget[] = []): ProfileDisplayTarget {
  const target = { ...profileTargetFromDisplay(display, fallbackModes), enabled: true, isPrimary: isFirstDisplay };
  if (isFirstDisplay || existingTargets.length === 0) return target;

  const rects = existingTargets.map(existing => rectFromPosition(existing.positionX, existing.positionY, existing.width, existing.height));
  const anchorIndex = Math.max(existingTargets.findIndex(existing => existing.isPrimary), 0);
  const positioned = placeRectNextToTopology(rectFromPosition(target.positionX, target.positionY, target.width, target.height), rects, anchorIndex);
  return { ...target, positionX: positioned.left, positionY: positioned.top };
}

export function getTopologyEditorErrors(topology: GamingProfileDisplayTopology, translate?: (key: string) => string): string[] {
  return topology.displays.length === 0 ? [] : validateTopology(topology, translate);
}

export function normalizeTopologyPrimary(topology: GamingProfileDisplayTopology): GamingProfileDisplayTopology {
  const enabled = topology.displays.find((display) => display.enabled && display.isPrimary) ?? topology.displays.find((display) => display.enabled);
  return { displays: topology.displays.map((display) => ({ ...display, isPrimary: Boolean(enabled && sameIdentity(display.identity, enabled.identity) && display.enabled) })) };
}

export function getProfileTopology(profile: DisplayLayoutProfile): GamingProfileDisplayTopology {
  return profile.displayTopology ?? topologyFromLegacy(profile.layout);
}

export function topologyToLegacy(topology: GamingProfileDisplayTopology, observedDisplays: DisplayDeviceInfo[]): DisplayLayoutSnapshot {
  return {
    id: `${Date.now()}`,
    capturedAt: new Date().toISOString(),
    displays: topology.displays.map((display) => {
      const observed = observedDisplays.find((candidate) => sameIdentity(candidate.identity, display.identity));
      return {
        identity: display.identity,
        isActive: display.enabled,
        isAvailable: observed?.isAvailable ?? false,
        isPrimary: display.isPrimary,
        positionX: display.positionX,
        positionY: display.positionY,
        width: display.width,
        height: display.height,
        orientation: display.orientation,
        refreshRateNumerator: display.refreshPolicy === 'Fixed' ? display.fixedRefreshRateNumerator ?? 0 : 0,
        refreshRateDenominator: display.refreshPolicy === 'Fixed' ? display.fixedRefreshRateDenominator ?? 0 : 0,
        refreshRateHz: display.refreshRateDenominator ? display.refreshRateNumerator / display.refreshRateDenominator : 0,
        currentModeAvailable: observed?.currentModeAvailable ?? false,
        preferredWidth: observed?.preferredWidth ?? 0,
        preferredHeight: observed?.preferredHeight ?? 0,
        sourceId: observed?.sourceId ?? 0,
        targetId: observed?.targetId ?? display.identity.targetId,
        adapterLuid: observed?.adapterLuid ?? display.identity.adapterLuid,
        sourceDeviceName: observed?.sourceDeviceName ?? '',
        connectionTechnology: observed?.connectionTechnology ?? ''
      };
    })
  };
}

export function areTopologiesEqual(left: GamingProfileDisplayTopology, right: GamingProfileDisplayTopology) {
  return JSON.stringify(left) === JSON.stringify(right);
}

const defaultTopologyValidationMessages: Record<string, string> = {
  'profile.validationMinDisplays': 'Add at least one display.',
  'profile.validationNoEnabledDisplays': 'Enable at least one display.',
  'profile.validationSinglePrimary': 'Select exactly one primary display.',
  'profile.validationDisabledPrimary': 'A disabled display cannot be the primary display.',
  'profile.validationInvalidResolution': 'Every enabled display must have a valid resolution.',
  'profile.validationInvalidRefresh': 'Choose a valid fixed refresh rate for this display.'
};

export function validateTopology(topology: GamingProfileDisplayTopology, translate?: (key: string) => string): string[] {
  const t = translate ?? ((key: string) => defaultTopologyValidationMessages[key] ?? key);
  const errors: string[] = [];
  if (topology.displays.length === 0) errors.push(t('profile.validationMinDisplays'));
  const enabled = topology.displays.filter((display) => display.enabled);
  if (enabled.length === 0) errors.push(t('profile.validationNoEnabledDisplays'));
  if (enabled.filter((display) => display.isPrimary).length !== 1) errors.push(t('profile.validationSinglePrimary'));
  if (topology.displays.some((display) => !display.enabled && display.isPrimary)) errors.push(t('profile.validationDisabledPrimary'));
  if (enabled.some((display) => display.width <= 0 || display.height <= 0)) errors.push(t('profile.validationInvalidResolution'));
  if (enabled.some((display) => getRefreshPolicy(display) === 'Fixed' && !hasValidFixedRefresh(display))) errors.push(t('profile.validationInvalidRefresh'));
  return errors;
}

export function extractDisplayIdentities(layout: DisplayLayoutSnapshot) {
  return layout.displays.map((display) => display.identity);
}

export function sameIdentity(left: DisplayIdentity, right: DisplayIdentity) {
  const leftPath = left.monitorDevicePath?.trim();
  const rightPath = right.monitorDevicePath?.trim();
  if (leftPath && rightPath && leftPath.toLowerCase() === rightPath.toLowerCase()) {
    return true;
  }

  if (left.adapterLuid !== 0
    && right.adapterLuid !== 0
    && left.targetId !== 0
    && right.targetId !== 0) {
    return left.adapterLuid === right.adapterLuid
      && left.targetId === right.targetId;
  }

  if (left.edidManufacturerId > 0
    && right.edidManufacturerId > 0
    && left.edidProductCodeId > 0
    && right.edidProductCodeId > 0
    && left.connectorInstance > 0
    && right.connectorInstance > 0) {
    return left.edidManufacturerId === right.edidManufacturerId
      && left.edidProductCodeId === right.edidProductCodeId
      && left.connectorInstance === right.connectorInstance;
  }

  return false;
}

export function areIdentitySetsEqual(left: DisplayIdentity[], right: DisplayIdentity[]) {
  if (left.length !== right.length) {
    return false;
  }

  return left.every((identity) => right.some((candidate) => sameIdentity(identity, candidate)));
}

type TranslationLike = (key: string) => string;

export function summarizeLayout(layout: DisplayLayoutSnapshot, t?: TranslationLike) {
  const active = layout.displays.filter((display) => display.isActive);
  if (active.length === 0) {
    return t ? t('profile.summaryNoActiveDisplays') : 'No active displays';
  }

  return active.map((display) => `${display.identity.friendlyName || (t ? t('profile.displayUnknown') : 'Unknown display')} ${display.width} × ${display.height}`).join(' / ');
}

export function formatDisplayResolution(display: { width: number; height: number; preferredWidth?: number; preferredHeight?: number }, t?: TranslationLike) {
  const width = display.width > 0 ? display.width : display.preferredWidth ?? 0;
  const height = display.height > 0 ? display.height : display.preferredHeight ?? 0;
  return width > 0 && height > 0 ? `${width} × ${height}` : (t ? t('profile.resolutionUnknown') : 'Resolution unknown');
}

export function formatDisplayRefresh(display: { refreshRateHz: number }, t?: TranslationLike) {
  if (!display.refreshRateHz || display.refreshRateHz <= 0) {
    return t ? t('profile.refreshUnknown') : 'Refresh unknown';
  }

  return `${formatNumber(display.refreshRateHz)} Hz`;
}

function formatNumber(value: number) {
  return Number.isInteger(value) ? value.toFixed(0) : value.toFixed(2).replace(/\.?0+$/, '');
}
