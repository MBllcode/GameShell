import { SettingRow } from './SettingRow';
import { SettingsSection } from './SettingsSection';

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
      <p>Ustaw, jak ma wyglądać środowisko pracy po powrocie do trybu roboczego. Te ustawienia pomagają przywrócić wygodny stan systemu po sesji gamingowej.</p>
      <div className="settings-category-list">
        <SettingsSection title="LAUNCHER">
          <SettingRow title="Zamknij launcher" checked={behaviour.closeLauncher} onChange={(value) => setBehaviour({ ...behaviour, closeLauncher: value })} />
        </SettingsSection>
        <SettingsSection title="PULPIT">
          <SettingRow title="Pokaż paski zadań" checked={behaviour.showTaskbars} onChange={(value) => setBehaviour({ ...behaviour, showTaskbars: value })} />
          <SettingRow title="Pokaż ikony pulpitu" checked={behaviour.showDesktopIcons} onChange={(value) => setBehaviour({ ...behaviour, showDesktopIcons: value })} />
        </SettingsSection>
        <SettingsSection title="OKNA">
          <SettingRow title="Przywróć otwarte okna" checked={behaviour.doNotMinimizeWindows} onChange={(value) => setBehaviour({ ...behaviour, doNotMinimizeWindows: value })} />
        </SettingsSection>
      </div>
    </div>
  );
}
