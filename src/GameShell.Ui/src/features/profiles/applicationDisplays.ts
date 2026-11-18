import type { ProfileDisplayTarget } from '../../bridge/contracts';

export const applicationDisplayId = (display: ProfileDisplayTarget) => display.knownPhysicalDisplayId || display.identity.monitorDevicePath;
export function findApplicationDisplay(id: string, displays: ProfileDisplayTarget[]) {
  const normalized = id.toLowerCase();
  return displays.find(display => [display.knownPhysicalDisplayId, display.identity.monitorDevicePath]
    .some(candidate => Boolean(candidate) && candidate!.toLowerCase() === normalized));
}
