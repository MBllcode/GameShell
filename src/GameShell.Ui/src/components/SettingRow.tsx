import type { ReactNode } from 'react';
import { IconBadge, UiIcon, type UiIconName } from './UiPrimitives';

export function SettingRow({
  title,
  description,
  checked,
  onChange,
  disabled = false,
  trailing,
  helper,
  icon,
  showInfo = false,
  className
}: {
  title: string;
  description?: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
  trailing?: ReactNode;
  helper?: ReactNode;
  icon?: UiIconName;
  showInfo?: boolean;
  className?: string;
}) {
  const rowClasses = [
    'setting-row',
    disabled ? 'is-disabled' : '',
    trailing || showInfo ? 'has-trailing' : '',
    icon ? 'has-icon' : '',
    showInfo ? 'has-info' : '',
    helper ? 'has-helper' : '',
    className ?? ''
  ].filter(Boolean).join(' ');

  return (
    <label className={rowClasses}>
      <GameShellCheckbox checked={checked} disabled={disabled} onChange={onChange} />
      {icon ? <IconBadge name={icon} size={16} className="setting-row-icon" /> : null}
      <span className="setting-row-content">
        <span className="setting-row-title">{title}</span>
        {description ? <span className="setting-row-description">{description}</span> : null}
      </span>
      {trailing || showInfo ? (
        <span className="setting-row-trailing">
          {trailing}
          {showInfo ? <span className="setting-row-info" aria-hidden="true"><UiIcon name="info" size={15} /></span> : null}
        </span>
      ) : null}
      {helper ? <span className="setting-row-helper" role="status">{helper}</span> : null}
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
