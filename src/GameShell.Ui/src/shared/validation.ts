export function scheduleValidationErrorClear<T>(
  value: T,
  onValueChange: (nextValue: T | null) => void,
  delayMs = 2500
) {
  let timeoutId: ReturnType<typeof setTimeout> | null = null;

  onValueChange(value);

  timeoutId = setTimeout(() => {
    onValueChange(null);
    timeoutId = null;
  }, delayMs);

  return () => {
    if (timeoutId) {
      clearTimeout(timeoutId);
      timeoutId = null;
    }
  };
}

export function triggerValidationError<T extends string | null>(
  value: T,
  onValueChange: (nextValue: T | null) => void
) {
  if (value === null) {
    onValueChange(null);
    return;
  }

  onValueChange(null);
  queueMicrotask(() => {
    onValueChange(value);
  });
}
