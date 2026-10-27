import type { ReactNode } from 'react';
import { SectionFrame } from './UiPrimitives';

export function SettingsSection({
  title,
  children
}: {
  title: string;
  children: ReactNode;
}) {
  return <SectionFrame title={title} className="settings-category"><div className="settings-group">{children}</div></SectionFrame>;
}
