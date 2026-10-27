import type { AppSettings } from '../../bridge/contracts';

const WINDOW_STATE_KEYS = new Set(['windowLeft', 'windowTop', 'windowWidth', 'windowHeight']);
const DISPLAY_LAYOUT_METADATA_KEYS = new Set(['id', 'capturedAt']);

function normalizeForComparison(value: unknown, key?: string): unknown {
  if (WINDOW_STATE_KEYS.has(key ?? '') || DISPLAY_LAYOUT_METADATA_KEYS.has(key ?? '')) return undefined;
  if (Array.isArray(value)) return value.map((item) => normalizeForComparison(item));
  if (value && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value)
        .sort(([left], [right]) => left.localeCompare(right))
        .map(([entryKey, entryValue]) => [entryKey, normalizeForComparison(entryValue, entryKey)])
        .filter(([, entryValue]) => entryValue !== undefined)
    );
  }
  return value;
}

export function createSettingsComparisonKey(settings: AppSettings): string {
  return JSON.stringify(normalizeForComparison(settings));
}

export function areSettingsEquivalent(left: AppSettings, right: AppSettings): boolean {
  return createSettingsComparisonKey(left) === createSettingsComparisonKey(right);
}
