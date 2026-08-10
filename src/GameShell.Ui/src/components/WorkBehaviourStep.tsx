import { SettingRow } from './SettingRow';

type WorkBehaviour = {
  closeLauncher: boolean;
  showTaskbars: boolean;
  showDesktopIcons: boolean;
  doNotMinimizeWindows: boolean;
};

export function WorkBehaviourStep({
  stepNumber,
  behaviour,
  setBehaviour
}: {
  stepNumber: number;
  behaviour: WorkBehaviour;
  setBehaviour: (behaviour: WorkBehaviour) => void;
}) {
  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Zachowanie Work Mode</h2>
      <p>Ustaw wygląd pulpitu po powrocie do Work Mode.</p>
      <div className="settings-group">
        <SettingRow title="Zamknij launcher" checked={behaviour.closeLauncher} onChange={(value) => setBehaviour({ ...behaviour, closeLauncher: value })} />
        <SettingRow title="Pokaż paski zadań" checked={behaviour.showTaskbars} onChange={(value) => setBehaviour({ ...behaviour, showTaskbars: value })} />
        <SettingRow title="Pokaż ikony pulpitu" checked={behaviour.showDesktopIcons} onChange={(value) => setBehaviour({ ...behaviour, showDesktopIcons: value })} />
        <SettingRow title="Przywróć otwarte okna" checked={behaviour.doNotMinimizeWindows} onChange={(value) => setBehaviour({ ...behaviour, doNotMinimizeWindows: value })} />
      </div>
    </div>
  );
}
