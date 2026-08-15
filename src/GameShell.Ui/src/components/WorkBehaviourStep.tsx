import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation();

  return (
    <div className="wizard-copy">
      <p className="eyebrow">{t('setup.step')} {stepNumber}</p>
      <h2>{t('globalSection.workBehaviour')}</h2>
      <p>{t('behaviour.workDescription')}</p>
      <div className="settings-category-list">
        <SettingsSection title={t('behaviour.launcher')}>
          <SettingRow title={t('behaviour.closeLauncher')} checked={behaviour.closeLauncher} onChange={(value) => setBehaviour({ ...behaviour, closeLauncher: value })} />
        </SettingsSection>
        <SettingsSection title={t('behaviour.desktop')}>
          <SettingRow title={t('behaviour.showTaskbars')} checked={behaviour.showTaskbars} onChange={(value) => setBehaviour({ ...behaviour, showTaskbars: value })} />
          <SettingRow title={t('behaviour.showDesktopIcons')} checked={behaviour.showDesktopIcons} onChange={(value) => setBehaviour({ ...behaviour, showDesktopIcons: value })} />
        </SettingsSection>
        <SettingsSection title={t('behaviour.windows')}>
          <SettingRow title={t('behaviour.restoreOpenWindows')} checked={behaviour.doNotMinimizeWindows} onChange={(value) => setBehaviour({ ...behaviour, doNotMinimizeWindows: value })} />
        </SettingsSection>
      </div>
    </div>
  );
}
