import type { DisplayDeviceInfo, DisplayIdentity, DisplayLayoutProfile, DisplayLayoutSnapshot, DisplayModeInfo, GamingProfileDisplayTopology, ProfileDisplayTarget, RefreshPolicy } from '../types/bridge';

export const MAX_PROFILE_DISPLAYS = 5;
export const PROFILE_DISPLAY_LIMIT_MESSAGE = 'Profil może zawierać maksymalnie 5 ekranów.';

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

export function validateTopology(topology: GamingProfileDisplayTopology): string[] {
  const errors: string[] = [];
  if (topology.displays.length === 0) errors.push('Dodaj co najmniej jeden ekran.');
  if (topology.displays.length > MAX_PROFILE_DISPLAYS) errors.push(PROFILE_DISPLAY_LIMIT_MESSAGE);
  const enabled = topology.displays.filter((display) => display.enabled);
  if (enabled.length === 0) errors.push('Włącz co najmniej jeden ekran.');
  if (enabled.filter((display) => display.isPrimary).length !== 1) errors.push('Wybierz dokładnie jeden główny ekran.');
  if (topology.displays.some((display) => !display.enabled && display.isPrimary)) errors.push('Wyłączony ekran nie może być głównym ekranem.');
  if (enabled.some((display) => display.width <= 0 || display.height <= 0)) errors.push('Każdy włączony ekran musi mieć poprawną rozdzielczość.');
  if (enabled.some((display) => getRefreshPolicy(display) === 'Fixed' && !hasValidFixedRefresh(display))) errors.push('Wybierz poprawną stałą częstotliwość odświeżania dla tego ekranu.');
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

export function summarizeLayout(layout: DisplayLayoutSnapshot) {
  const active = layout.displays.filter((display) => display.isActive);
  if (active.length === 0) {
    return 'brak aktywnych ekranów';
  }

  return active.map((display) => `${display.identity.friendlyName || 'Ekran'} ${display.width} × ${display.height}`).join(' / ');
}

export function formatDisplayResolution(display: { width: number; height: number; preferredWidth?: number; preferredHeight?: number }) {
  const width = display.width > 0 ? display.width : display.preferredWidth ?? 0;
  const height = display.height > 0 ? display.height : display.preferredHeight ?? 0;
  return width > 0 && height > 0 ? `${width} × ${height}` : 'Rozdzielczość nieznana';
}

export function formatDisplayRefresh(display: { refreshRateHz: number }) {
  if (!display.refreshRateHz || display.refreshRateHz <= 0) {
    return 'Hz nieznane';
  }

  return `${formatNumber(display.refreshRateHz)} Hz`;
}

function formatNumber(value: number) {
  return Number.isInteger(value) ? value.toFixed(0) : value.toFixed(2).replace(/\.?0+$/, '');
}
