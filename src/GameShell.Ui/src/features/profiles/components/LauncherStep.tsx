import { MoveApplicationMenu } from './MoveApplicationMenu';
import { ProfileFilePicker } from './ProfileFilePicker';
import { applicationDisplayId, findApplicationDisplay } from '../applicationDisplays';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ProfileApplication, ProfileDisplayTarget } from '../../../bridge/contracts';
import { PageHeader, UiIcon } from '../../../shared/ui/UiPrimitives';
import type { LauncherType } from '../profileCommands';

export function LauncherStep({ stepNumber, launcher, removeLauncher, applications, onChange, displays, disabled }: {
  stepNumber: number;
  launcher: LauncherType;
  removeLauncher: () => void;
  applications: ProfileApplication[];
  onChange: (applications: ProfileApplication[]) => void;
  displays: ProfileDisplayTarget[];
  disabled: boolean;
}) {
  const { t } = useTranslation();
  const [pickTarget, setPickTarget] = useState<ProfileDisplayTarget | null>(null);
  const [draggedPath, setDraggedPath] = useState<string | null>(null);
  const [dropTarget, setDropTarget] = useState<string | null>(null);
  const [removed, setRemoved] = useState<{ app: ProfileApplication; index: number } | null>(null);
  const [notice, setNoticeText] = useState('');
  const [noticeVersion, setNoticeVersion] = useState(0);
  const [noticePaused, setNoticePaused] = useState(false);
  const setNotice = (text: string) => { setNoticeText(text); setNoticeVersion(value => value + 1); };
  useEffect(() => {
    if (!notice || noticePaused) return;
    const timer = window.setTimeout(() => { setNoticeText(''); setRemoved(null); }, removed ? 8000 : 3000);
    return () => window.clearTimeout(timer);
  }, [notice, noticeVersion, removed, noticePaused]);
  const picking = pickTarget !== null;
  const [error, setError] = useState<string | null>(null);
  const targets = displays.filter(display => display.enabled).sort((a, b) => a.positionX - b.positionX || a.positionY - b.positionY);
  const displayId = applicationDisplayId;
  const update = (index: number, patch: Partial<ProfileApplication>) => onChange(applications.map((app, i) => i === index ? { ...app, ...patch } : app));
  const add = (path: string) => {
    if (!pickTarget) return;
    if (applications.some(app => app.executablePath.toLowerCase() === path.toLowerCase())) {
      setError(t('applications.duplicate'));
    } else {
      onChange([...applications, { executablePath: path, displayId: displayId(pickTarget), runMinimized: false }]);
      setError(null);
    }
    setPickTarget(null);
  };
  const renderApplication = (app: ProfileApplication, index: number) => {
    const assigned = findApplicationDisplay(app.displayId, targets);
    const name = app.executablePath.split(/[\\/]/).pop()?.replace(/\.exe$/i, '') ?? app.executablePath;
    return <fieldset className="setup-app" key={app.executablePath} disabled={disabled || picking}>
      <legend className="setup-app-accessible-name">{name}</legend><div className="setup-app-heading"><span className="setup-app-identity" draggable={!disabled && !picking} onDragStart={event => { event.dataTransfer.setData('application/x-gameshell-app', app.executablePath); event.dataTransfer.effectAllowed = 'move'; setDraggedPath(app.executablePath); }} onDragEnd={() => { setDraggedPath(null); setDropTarget(null); }} title={t('applications.drag')}><span className="setup-app-grip" aria-hidden="true">⠿</span>{name}</span></div>
      <p className="application-path">{app.executablePath}</p>
      <div className="setup-app-mode-label">{t('applications.visibility')}</div>
      <div className="setup-app-modes" role="group" aria-label={t('applications.visibility')}>
        <button type="button" aria-pressed={!app.runMinimized} onClick={() => update(index, { runMinimized: false })}><UiIcon name="check" size={14} />{t('applications.modeVisible')}</button>
        <button type="button" aria-pressed={app.runMinimized} onClick={() => update(index, { runMinimized: true })}><UiIcon name="check" size={14} />{t('applications.modeBackground')}</button>
      </div>
      <p className="setup-app-mode-description" aria-live="polite">{t(app.runMinimized ? 'applications.modeBackgroundHint' : 'applications.modeVisibleHint')}</p>
      <div className="setup-app-controls">
      <MoveApplicationMenu label={t('applications.moveAction')} disabled={disabled || picking}
        targets={targets.filter(display => display !== assigned).map(display => ({ id: displayId(display), name: display.identity.friendlyName }))}
        onMove={id => { update(index, { displayId: id }); setRemoved(null); setNotice(t('applications.moved', { display: targets.find(display => displayId(display) === id)?.identity.friendlyName ?? '' })); }} />
      <div className="setup-app-footer">
        <button className="setup-app-remove" type="button" aria-label={t('applications.removeNamed', { name })} onClick={() => { setRemoved({ app, index }); onChange(applications.filter((_, i) => i !== index)); setNotice(t('applications.removed', { name })); }}>{t('applications.remove')}</button>
      </div>
      </div>
    </fieldset>;
  };
  const unassigned = applications.filter(app => !findApplicationDisplay(app.displayId, targets));
  return <div className="wizard-copy application-workspace">
    {pickTarget ? <ProfileFilePicker onPick={add} onClose={() => setPickTarget(null)} /> : null}
    <PageHeader eyebrow={`${t('setup.step')} ${stepNumber}`} title={t('launcher.title')} description={t('applications.intro')} />
    {launcher !== 'None' ? <div className="application-card"><p>{t('applications.legacy', { name: launcher })}</p><button type="button" className="secondary-button" disabled={disabled} onClick={removeLauncher}>{t('applications.remove')}</button></div> : null}
    <div className="setup-workspace-toolbar"><span>{t('applications.dragHint')}</span></div>
    <div className="setup-monitor-grid">
      {targets.map((display, index) => {
        const assigned = applications.filter(app => findApplicationDisplay(app.displayId, targets) === display);
        return <section className={`setup-monitor ${dropTarget === displayId(display) ? 'setup-monitor--drop' : ''}`} key={displayId(display)} aria-label={display.identity.friendlyName}
          onDragOver={event => { if (draggedPath && !disabled && !picking) { event.preventDefault(); event.dataTransfer.dropEffect = 'move'; setDropTarget(displayId(display)); } }}
          onDragLeave={event => { if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setDropTarget(null); }}
          onDrop={event => {
            event.preventDefault();
            const path = event.dataTransfer.getData('application/x-gameshell-app');
            if (!disabled && !picking && draggedPath === path) {
              const index = applications.findIndex(app => app.executablePath === path);
              if (index >= 0) { update(index, { displayId: displayId(display) }); setRemoved(null); setNotice(t('applications.moved', { display: display.identity.friendlyName })); }
            }
            setDraggedPath(null); setDropTarget(null);
          }}>
          <header className="setup-monitor-heading">
            <span className="setup-monitor-number" aria-hidden="true">{index + 1}</span>
            <div><h3>{display.identity.friendlyName}</h3><p>{display.width} × {display.height}{display.isPrimary ? ` · ${t('applications.primary')}` : ''}</p></div>
          </header>
          <div className="setup-monitor-body">
            {assigned.length ? assigned.map(app => renderApplication(app, applications.indexOf(app))) : <div className="setup-monitor-empty"><strong>{t('applications.emptyDisplay')}</strong><p>{t('applications.emptyDisplayHint')}</p></div>}
            <button className="setup-monitor-add" type="button" disabled={disabled || picking} onClick={() => setPickTarget(display)}>+ {t(picking ? 'applications.picking' : 'applications.add')}</button>
          </div>
        </section>;
      })}
    </div>
    {unassigned.length ? <section className="setup-unassigned" aria-label={t('applications.unassigned')}><h3>{t('applications.unassigned')}</h3><p>{t('applications.unassignedHint')}</p><div className="setup-monitor-grid">{unassigned.map(app => renderApplication(app, applications.indexOf(app)))}</div></section> : null}
    {!targets.length ? <p role="status">{t('applications.noDisplays')}</p> : null}
    {notice ? <div className="setup-workspace-notice" onMouseEnter={() => setNoticePaused(true)} onMouseLeave={() => setNoticePaused(false)} onFocus={() => setNoticePaused(true)} onBlur={event => { if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setNoticePaused(false); }}><span role="status">{notice}</span>{removed ? <button type="button" className="secondary-button" disabled={disabled || picking} onClick={() => {
      if (!applications.some(app => app.executablePath.toLowerCase() === removed.app.executablePath.toLowerCase())) {
        const next = [...applications]; next.splice(Math.min(removed.index, next.length), 0, removed.app); onChange(next);
      }
      setRemoved(null); setNotice(t('applications.restored'));
    }}>{t('applications.undo')}</button> : null}</div> : null}
    {error ? <p className="error-line" role="alert">{error}</p> : null}
  </div>;
}
