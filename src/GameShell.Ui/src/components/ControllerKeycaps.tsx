import { ControllerFamily, getControllerButtonDisplayName } from '../controllerDisplayMapper';
import { formatControllerShortcutButtons } from '../utils/controllers';

export function ControllerKeycaps({ buttons, family, hasError = false }: { buttons: string[]; family: ControllerFamily; hasError?: boolean }) {
  if (buttons.length === 0) {
    return null;
  }

  return (
    <div className={`controller-keycaps ${hasError ? 'is-invalid' : ''}`} aria-label={`Kombinacja: ${formatControllerShortcutButtons(buttons, family)}`}>
      {buttons.map((button) => <span className="controller-keycap" key={button}>{getControllerButtonDisplayName(button, family)}</span>)}
    </div>
  );
}
