export function SettingRow({
  title,
  description,
  checked,
  onChange,
  disabled = false
}: {
  title: string;
  description?: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
}) {
  return (
    <label className={`setting-row ${disabled ? 'is-disabled' : ''}`}>
      <GameShellCheckbox checked={checked} disabled={disabled} onChange={onChange} />
      <span className="setting-row-content">
        <span className="setting-row-title">{title}</span>
        {description ? <span className="setting-row-description">{description}</span> : null}
      </span>
    </label>
  );
}

function GameShellCheckbox({
  checked,
  disabled,
  onChange
}: {
  checked: boolean;
  disabled?: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    <>
      <input
        className="setting-checkbox-native"
        type="checkbox"
        checked={checked}
        disabled={disabled}
        onChange={(event) => onChange(event.target.checked)}
        onKeyDown={(event) => {
          if (event.key !== 'Enter') {
            return;
          }

          event.preventDefault();
          onChange(!checked);
        }}
      />
      <span className="setting-checkbox-visual" aria-hidden="true" />
    </>
  );
}
