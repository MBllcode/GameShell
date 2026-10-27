import type { ReactNode } from 'react';

export type UiIconName =
  | 'globe'
  | 'display'
  | 'launcher'
  | 'controller'
  | 'desktop'
  | 'keyboard'
  | 'startup'
  | 'minimize'
  | 'settings'
  | 'shortcut'
  | 'dashboard'
  | 'save'
  | 'profile'
  | 'check'
  | 'plus'
  | 'play'
  | 'trash'
  | 'refresh'
  | 'layout'
  | 'star'
  | 'info'
  | 'warning'
  | 'desktopOff'
  | 'taskbar'
  | 'cursor'
  | 'cursorOff'
  | 'windowClose'
  | 'windowMinimize'
  | 'foreground';

const iconPaths: Record<UiIconName, ReactNode> = {
  globe: <><circle cx="12" cy="12" r="8.4" /><path d="M3.9 12h16.2M12 3.6c2.1 2.2 3.1 5 3.1 8.4S14.1 18.2 12 20.4M12 3.6c-2.1 2.2-3.1 5-3.1 8.4s1 6.2 3.1 8.4" /></>,
  display: <><rect x="3.2" y="4.5" width="17.6" height="12" rx="1.2" /><path d="M8.5 20h7M12 16.5V20" /></>,
  launcher: <><rect x="4" y="3.6" width="16" height="16.8" rx="2" /><path d="M8 8h8M8 12h2M12 12h4M8 16h8" /></>,
  controller: <><path d="M7.2 8.2h9.6c2.1 0 3.3 1.6 4 4.9.5 2.6-.1 4.2-1.6 4.2-1.2 0-2.1-1.2-3.4-2.7H8.2c-1.3 1.5-2.2 2.7-3.4 2.7-1.5 0-2.1-1.6-1.6-4.2.7-3.3 1.9-4.9 4-4.9Z" /><path d="M7.5 11v3M6 12.5h3M16.5 12.5h.01M18.5 14.5h.01" /></>,
  desktop: <><rect x="3.4" y="4.2" width="17.2" height="13.2" rx="1.3" /><path d="M8 20h8M12 17.4V20" /></>,
  keyboard: <><rect x="3" y="6.6" width="18" height="10.8" rx="1.6" /><path d="M6.2 10h.01M9 10h.01M11.8 10h.01M14.6 10h.01M17.4 10h.01M6.2 13.8h8.8M17.4 13.8h.01" /></>,
  startup: <><path d="M12 3.2v4M12 16.8v4M3.2 12h4M16.8 12h4M5.8 5.8l2.8 2.8M15.4 15.4l2.8 2.8M18.2 5.8l-2.8 2.8M8.6 15.4l-2.8 2.8" /><circle cx="12" cy="12" r="4.1" /></>,
  minimize: <><path d="M4 8.5h16M8 4.5v4M12 4.5v4M16 4.5v4M4 19.5h16" /><path d="M9 14h6M12 11v6" /></>,
  settings: <><path d="m12 3.4 1.3 1.9 2.3.5 1.5-1 1.7 1.7-1 1.5.5 2.3 1.9 1.3v2.4l-1.9 1.3-.5 2.3 1 1.5-1.7 1.7-1.5-1-2.3.5L12 20.6l-1.3-1.9-2.3-.5-1.5 1-1.7-1.7 1-1.5-.5-2.3-1.9-1.3V10l1.9-1.3.5-2.3-1-1.5 1.7-1.7 1.5 1 2.3-.5L12 3.4Z" /><circle cx="12" cy="12" r="2.5" /></>,
  shortcut: <><path d="M10.2 13.8 8.7 15.3a3.1 3.1 0 0 1-4.4-4.4l2.4-2.4a3.1 3.1 0 0 1 4.4 0" /><path d="m13.8 10.2 1.5-1.5a3.1 3.1 0 0 1 4.4 4.4l-2.4 2.4a3.1 3.1 0 0 1-4.4 0" /><path d="m8.8 15.2 6.4-6.4" /></>,
  dashboard: <><rect x="3.5" y="3.5" width="6.2" height="6.2" rx=".7" /><rect x="14.3" y="3.5" width="6.2" height="6.2" rx=".7" /><rect x="3.5" y="14.3" width="6.2" height="6.2" rx=".7" /><rect x="14.3" y="14.3" width="6.2" height="6.2" rx=".7" /></>,
  save: <><path d="M4 3.5h12.5l3.5 3.5v13.5H4Z" /><path d="M7.5 3.5v6h8v-6M8 20.5v-6.2h8v6.2" /></>,
  profile: <><rect x="4" y="5" width="13" height="14" rx="1.4" /><path d="M7 8h7M7 11.5h5M7 15h3" /><path d="M17 8.5h2.2A1.8 1.8 0 0 1 21 10.3v7.2a1.5 1.5 0 0 1-1.5 1.5H9.2" /></>,
  check: <path d="m5.2 12.4 4.3 4.3 9.5-10" />,
  plus: <><path d="M12 4v16M4 12h16" /></>,
  play: <path d="m9 5 10 7-10 7V5Z" />,
  trash: <><path d="M4.5 7.5h15M9 7.5V4h6v3.5M7 7.5l.8 12h8.4l.8-12M10 11v5.5M14 11v5.5" /></>,
  refresh: <><path d="M20 11a8 8 0 0 0-14.8-4L4 9M4 5v4h4M4 13a8 8 0 0 0 14.8 4L20 15M20 19v-4h-4" /></>,
  layout: <><rect x="3.5" y="4" width="7" height="7" rx=".8" /><rect x="13.5" y="4" width="7" height="7" rx=".8" /><rect x="3.5" y="13" width="7" height="7" rx=".8" /><rect x="13.5" y="13" width="7" height="7" rx=".8" /></>,
  star: <path d="m12 3.5 2.6 5.3 5.9.9-4.3 4.2 1 5.9-5.2-2.8-5.2 2.8 1-5.9-4.3-4.2 5.9-.9L12 3.5Z" />,
  info: <><circle cx="12" cy="12" r="8.5" /><path d="M12 10.8v5M12 7.8h.01" /></>,
  warning: <><path d="m12 4 8.4 15H3.6L12 4Z" /><path d="M12 9v4.5M12 16.3h.01" /></>,
  desktopOff: <><rect x="3.2" y="4.2" width="17.6" height="13.2" rx="1.3" /><path d="M8 20h8M12 17.4V20M4.5 4.5l15 15" /></>,
  taskbar: <><rect x="3.3" y="4" width="17.4" height="16" rx="1.3" /><path d="M3.5 16.2h16.8M7 19.2h.01M10 19.2h.01M13 19.2h.01M16 19.2h.01" /></>,
  cursor: <path d="m5 3.8 13.7 11.7-6.5.6 3.1 4.1-2 1.4-3.1-4.1-3.1 5.7L5 3.8Z" />,
  cursorOff: <><path d="m5 3.8 13.7 11.7-6.5.6 3.1 4.1-2 1.4-3.1-4.1-3.1 5.7L5 3.8Z" /><path d="m4 4 16 16" /></>,
  windowClose: <><rect x="3.3" y="4.2" width="17.4" height="15.6" rx="1.3" /><path d="M3.8 8.3h16.4M8.2 12.2l7.6 5.8M15.8 12.2l-7.6 5.8" /></>,
  windowMinimize: <><rect x="3.3" y="4.2" width="17.4" height="15.6" rx="1.3" /><path d="M3.8 8.3h16.4M8 16h8" /></>,
  foreground: <><rect x="3.5" y="7" width="13.5" height="13" rx="1.2" /><rect x="7" y="3.8" width="13.5" height="13" rx="1.2" /><path d="M9.8 8.3h7.8" /></>
};

export function UiIcon({ name, size = 20, className }: { name: UiIconName; size?: number; className?: string }) {
  return (
    <svg className={className ? `ui-icon ${className}` : 'ui-icon'} width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      {iconPaths[name]}
    </svg>
  );
}

export function IconBadge({ name, size = 18, className = '' }: { name: UiIconName; size?: number; className?: string }) {
  return <span className={`icon-badge ${className}`.trim()}><UiIcon name={name} size={size} /></span>;
}

export function StatusBadge({ children, tone = 'neutral', className = '' }: { children: ReactNode; tone?: 'neutral' | 'active' | 'danger' | 'warning'; className?: string }) {
  return <span className={`status-badge status-badge-${tone} ${className}`.trim()}>{children}</span>;
}

export function PageHeader({ eyebrow, title, description, icon }: { eyebrow?: string; title: string; description?: string; icon?: UiIconName }) {
  return (
    <header className="page-header">
      {eyebrow ? <p className="eyebrow page-header-eyebrow">{eyebrow}</p> : null}
      <div className="page-header-title-row">
        <span className="page-header-accent" aria-hidden="true" />
        {icon ? <UiIcon name={icon} size={24} className="page-header-icon" /> : null}
        <h2>{title}</h2>
      </div>
      {description ? <p className="page-header-description">{description}</p> : null}
    </header>
  );
}

export function SectionFrame({ title, children, className = '' }: { title: string; children: ReactNode; className?: string }) {
  return (
    <section className={`section-frame ${className}`.trim()} aria-label={title}>
      <div className="section-frame-heading"><h3>{title}</h3><span aria-hidden="true" /></div>
      <div className="section-frame-body">{children}</div>
    </section>
  );
}

export function ConfigCard({ children, className = '' }: { children: ReactNode; className?: string }) {
  return <section className={`config-card ${className}`.trim()}>{children}</section>;
}
