import type { DisplayDeviceInfo, DisplayIdentity } from '../../types/bridge';

export function syncActiveDisplaySelections(detectedDisplays: DisplayDeviceInfo[]): { consoleDisplays: DisplayIdentity[]; primaryDisplay: DisplayIdentity | null } {
  const activeDisplays = detectedDisplays.filter((display) => display.isEnabled);
  const consoleDisplays = activeDisplays.map((display) => display.identity);
  const primaryDisplay = activeDisplays.find((display) => display.isPrimary) ?? activeDisplays[0] ?? null;
  return { consoleDisplays, primaryDisplay: primaryDisplay?.identity ?? null };
}
