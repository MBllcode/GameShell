import type { ReactNode } from 'react';

export function SettingsSection({
  title,
  children
}: {
  title: string;
  children: ReactNode;
}) {
  return (
    <section className="settings-category" aria-labelledby={`settings-category-${title}`}>
      <h3 id={`settings-category-${title}`}>{title}</h3>
      <div className="settings-group">{children}</div>
    </section>
  );
}
