import { SettingRow } from './SettingRow';

type FinishOptions = {
  startWithWindows: boolean;
  startMinimized: boolean;
  startMenuShortcut: boolean;
  desktopShortcut: boolean;
};

export function AppBehaviourStep({
  stepNumber,
  finishOptions,
  setFinishOptions
}: {
  stepNumber: number;
  finishOptions: FinishOptions;
  setFinishOptions: (options: FinishOptions) => void;
}) {
  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Zachowanie aplikacji</h2>
      <p>Ustaw sposób uruchamiania GameShell oraz integrację z systemem Windows. Te opcje dotyczą automatycznego startu i dostępności aplikacji po restarcie systemu.</p>
      <div className="settings-group">
        <SettingRow
          title="Uruchamiaj z Windows"
          checked={finishOptions.startWithWindows}
          onChange={(value) => setFinishOptions({
            ...finishOptions,
            startWithWindows: value,
            startMinimized: value ? finishOptions.startMinimized : false
          })}
        />
        <SettingRow
          title="Uruchamiaj zminimalizowany"
          checked={finishOptions.startMinimized}
          disabled={!finishOptions.startWithWindows}
          onChange={(value) => setFinishOptions({ ...finishOptions, startMinimized: value })}
        />
        <SettingRow
          title="Utwórz skrót w menu Start"
          checked={finishOptions.startMenuShortcut}
          onChange={(value) => setFinishOptions({ ...finishOptions, startMenuShortcut: value })}
        />
        <SettingRow
          title="Utwórz skrót na pulpicie"
          checked={finishOptions.desktopShortcut}
          onChange={(value) => setFinishOptions({ ...finishOptions, desktopShortcut: value })}
        />
      </div>
    </div>
  );
}
