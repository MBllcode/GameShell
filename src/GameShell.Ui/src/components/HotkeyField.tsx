import { useEffect, useId, useState } from 'react';
import { sendBridgeCommand } from '../services/webViewBridge';
import { formatHotkey, isClearHotkeyEvent } from '../utils/hotkeys';

export function HotkeyField({
  value,
  onChange,
  placeholder,
  disabled = false,
  error = null
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
  disabled?: boolean;
  error?: string | null;
}) {
  const hintId = useId();
  const errorId = useId();
  const [isListening, setIsListening] = useState(false);
  const hasValue = value.trim().length > 0;

  useEffect(() => {
    return () => {
      void sendBridgeCommand('setHotkeyCaptureActive', { active: false }).catch(() => {
        // Ignore bridge teardown errors.
      });
    };
  }, []);

  const helperText = isListening
    ? 'Naciśnij kombinację klawiszy…'
    : hasValue
      ? 'Zapisany skrót klawiaturowy.'
      : 'Kliknij i naciśnij skrót';

  return (
    <div className={`hotkey-field ${isListening ? 'is-listening' : ''} ${disabled ? 'is-disabled' : ''}`}>
      <div className="hotkey-field-input-wrap">
        <input
          className="hotkey-input"
          value={value}
          readOnly
          disabled={disabled}
          placeholder={placeholder}
          aria-invalid={error ? 'true' : 'false'}
          aria-describedby={error ? `${hintId} ${errorId}` : hintId}
          onMouseDown={() => {
            void sendBridgeCommand('setHotkeyCaptureActive', { active: true }).catch(() => {
              // Bridge may be unavailable (e.g. preview mode); ignore.
            });
          }}
          onFocus={(event) => {
            void sendBridgeCommand('setHotkeyCaptureActive', { active: true }).catch(() => {
              // Bridge may be unavailable (e.g. preview mode); ignore and keep local capture behavior.
            });
            setIsListening(true);
            event.currentTarget.select();
          }}
          onBlur={() => {
            void sendBridgeCommand('setHotkeyCaptureActive', { active: false }).catch(() => {
              // Ignore bridge sync errors.
            });
            setIsListening(false);
          }}
          onKeyDown={(event) => {
            if (disabled) {
              return;
            }

            event.preventDefault();
            event.stopPropagation();
            if (isClearHotkeyEvent(event)) {
              onChange('');
              return;
            }

            const hotkey = formatHotkey(event);
            if (hotkey) {
              onChange(hotkey);
            }
          }}
          onPaste={(event) => event.preventDefault()}
          onDrop={(event) => event.preventDefault()}
        />
        {isListening ? <span className="hotkey-listening-badge">NASŁUCHIWANIE</span> : null}
      </div>
      <p id={hintId} className="hotkey-field-hint">{helperText}</p>
      {error ? (
        <div id={errorId} className="field-validation-error" role="alert">
          {error}
        </div>
      ) : null}
    </div>
  );
}
