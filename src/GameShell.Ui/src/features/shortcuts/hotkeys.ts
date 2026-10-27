import type { KeyboardEvent as ReactKeyboardEvent } from 'react';

export function isClearHotkeyEvent(event: ReactKeyboardEvent<HTMLInputElement>) {
  return !event.ctrlKey && !event.altKey && !event.shiftKey && !event.metaKey && ['Backspace', 'Delete', 'Escape'].includes(event.key);
}

export function formatHotkey(event: ReactKeyboardEvent<HTMLInputElement>) {
  const key = normalizeHotkeyKey(event.key, event.code);
  if (!key) {
    return null;
  }

  const modifiers = [
    event.ctrlKey ? 'Ctrl' : null,
    event.altKey ? 'Alt' : null,
    event.shiftKey ? 'Shift' : null,
    event.metaKey ? 'Win' : null
  ].filter(Boolean);

  return modifiers.length === 0 ? null : [...modifiers, key].join('+');
}

function normalizeHotkeyKey(key: string, code: string) {
  if (['Control', 'Shift', 'Alt', 'Meta', 'OS'].includes(key)) {
    return null;
  }

  if (/^Key[A-Z]$/.test(code)) {
    return code.slice(3);
  }

  if (/^Digit[0-9]$/.test(code)) {
    return code.slice(5);
  }

  if (key.length === 1) {
    return removeDiacritics(key).toUpperCase();
  }

  const aliases: Record<string, string> = {
    ' ': 'Space',
    ArrowLeft: 'Left',
    ArrowRight: 'Right',
    ArrowUp: 'Up',
    ArrowDown: 'Down',
    Backspace: 'Back',
    Enter: 'Return',
    Esc: 'Escape'
  };

  return aliases[key] ?? key;
}

export function removeDiacritics(value: string) {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '');
}

export function normalizeHotkeyValue(value: string) {
  return removeDiacritics(value.trim()).toUpperCase();
}

export function isSameHotkey(left: string, right: string) {
  const normalizedLeft = normalizeHotkeyValue(left);
  return normalizedLeft.length > 0 && normalizedLeft === normalizeHotkeyValue(right);
}

export function normalizeHotkeyForDisplay(value: string) {
  if (!value.trim()) {
    return '';
  }

  const parts = value
    .split('+')
    .map((part) => part.trim())
    .filter(Boolean);

  if (parts.length === 0) {
    return '';
  }

  const keyToken = removeDiacritics(parts[parts.length - 1]).toUpperCase();
  const lowerModifiers = new Set(parts.slice(0, -1).map((part) => removeDiacritics(part).toLowerCase()));
  const normalizedModifiers = [
    lowerModifiers.has('ctrl') || lowerModifiers.has('control') ? 'Ctrl' : null,
    lowerModifiers.has('alt') ? 'Alt' : null,
    lowerModifiers.has('shift') ? 'Shift' : null,
    lowerModifiers.has('win') || lowerModifiers.has('windows') || lowerModifiers.has('meta') ? 'Win' : null
  ].filter((modifier): modifier is string => Boolean(modifier));

  if (normalizedModifiers.length === 0 || !keyToken) {
    return '';
  }

  return [...normalizedModifiers, keyToken].join('+');
}
