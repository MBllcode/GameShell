import { ControllerFamily, getControllerButtonDisplayName } from '../controllerDisplayMapper';
import { formatControllerShortcutButtons } from '../utils/controllers';

export function ControllerKeycaps({ buttons, family }: { buttons: string[]; family: ControllerFamily }) {
  if (buttons.length === 0) {
    return null;
  }

  return (
    <div className="controller-keycaps" aria-label={`Kombinacja: ${formatControllerShortcutButtons(buttons, family)}`}>
      {buttons.map((button) => <span className="controller-keycap" key={button}>{getControllerButtonDisplayName(button, family)}</span>)}
    </div>
  );
}
