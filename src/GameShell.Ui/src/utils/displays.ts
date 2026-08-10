import type { DisplayIdentity, DisplayLayoutSnapshot } from '../types/bridge';

export const MAX_PROFILE_DISPLAYS = 2;
export const PROFILE_DISPLAY_LIMIT_MESSAGE = 'Profil może obecnie zawierać maksymalnie 2 ekrany.';

export function extractDisplayIdentities(layout: DisplayLayoutSnapshot) {
  return layout.displays
    .filter((display) => display.isActive)
    .map((display) => display.identity);
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
