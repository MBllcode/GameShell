import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { DisplayDeviceInfo, DisplayLayoutProfile, DisplayLayoutSnapshot, DisplayModeInfo, DisplayModeQueryResult, GamingProfileDisplayTopology, ProfileDisplayTarget } from '../types/bridge';
import { normalizeGamingProfileName } from '../features/profiles/utils';
import { computeTopologyBounds, createCanvasTransform, getDockingGuides, rectFromPosition, rectToCanvas, snapAndResolve, type CanvasTransform, type GeometryRect } from '../utils/displayTopologyGeometry';
import { areTopologiesEqual, deduplicatePhysicalDisplays, formatDisplayResolution, getProfileTopology, getRefreshPolicy, getRefreshSelectionState, getTopologyEditorErrors, hasValidFixedRefresh, isSamePhysicalDisplay, isSameProfileDisplay, normalizeCanvasTopology, normalizeTopologyPrimary, profileTargetForAddition, topologyFromLegacy } from '../utils/displays';
import { sendBridgeCommand } from '../services/webViewBridge';

type DisplayValidationState = { showNameError: boolean; showSelectionError: boolean };
type DragState = { identityKey: string; startX: number; startY: number; startRect: GeometryRect; currentRect: GeometryRect; transform: CanvasTransform; topologyRects: GeometryRect[]; hasMoved: boolean };

type DisplaysStepProps = {
  stepNumber: number;
  displays: DisplayDeviceInfo[];
  gamingProfiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  prefillFromSelectedProfile: boolean;
  onDraftChange: (profileName: string, topology: GamingProfileDisplayTopology) => void;
  validationState: DisplayValidationState;
  onValidationStateChange: (state: DisplayValidationState) => void;
  isRefreshingDisplays: boolean;
  isDisplaySettling: boolean;
  displayListError: string | null;
  refreshDisplays: () => Promise<void>;
  displayMode?: 'gaming' | 'work';
  workModeLayout?: DisplayLayoutSnapshot | null;
  workModeSavedLayout?: DisplayLayoutSnapshot | null;
  onWorkModeDraftChange?: (topology: GamingProfileDisplayTopology) => void;
};

export function DisplaysStep({
  stepNumber,
  displays,
  gamingProfiles,
  selectedProfileName,
  prefillFromSelectedProfile,
  onDraftChange,
  validationState,
  onValidationStateChange,
  isRefreshingDisplays,
  isDisplaySettling,
  displayListError,
  refreshDisplays,
  displayMode = 'gaming',
  workModeLayout = null,
  workModeSavedLayout = null,
  onWorkModeDraftChange
}: DisplaysStepProps) {
  const { t } = useTranslation();
  const workModeProfile = useMemo(() => displayMode === 'work' && workModeSavedLayout
    ? { name: t('setup.workMode'), layout: workModeSavedLayout, displayTopology: topologyFromLegacy(workModeSavedLayout) }
    : null, [displayMode, t, workModeSavedLayout]);
  const selectedProfile = workModeProfile ?? gamingProfiles.find((profile) => profile.name === selectedProfileName) ?? null;
  const [profileName, setProfileName] = useState('');
  const [topology, setTopology] = useState<GamingProfileDisplayTopology>({ displays: [] });
  const [selectedIdentityKey, setSelectedIdentityKey] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [selectedDisplayModes, setSelectedDisplayModes] = useState<DisplayModeInfo[]>([]);
  const lastPrefillKeyRef = useRef<string | null>(null);

  const prefillKey = useMemo(() => {
    if (!prefillFromSelectedProfile || !selectedProfile) return null;
    const layout = displayMode === 'work' ? workModeSavedLayout ?? selectedProfile.layout : selectedProfile.layout;
    return `${displayMode}:${selectedProfile.name}:${layout.id}:${layout.capturedAt}`;
  }, [displayMode, prefillFromSelectedProfile, selectedProfile, workModeSavedLayout]);

  useEffect(() => {
    if (!validationState.showSelectionError) return;
    const timeoutId = window.setTimeout(() => {
      onValidationStateChange({ ...validationState, showSelectionError: false });
    }, 2600);
    return () => window.clearTimeout(timeoutId);
  }, [onValidationStateChange, validationState]);

  useEffect(() => {
    if (!prefillFromSelectedProfile || !selectedProfile || !prefillKey) {
      lastPrefillKeyRef.current = null;
      return;
    }
    if (lastPrefillKeyRef.current === prefillKey) return;
    lastPrefillKeyRef.current = prefillKey;

    const sourceLayout = displayMode === 'work' ? workModeLayout ?? selectedProfile.layout : selectedProfile.layout;
    const nextTopology = displayMode === 'work'
      ? normalizeCanvasTopology(topologyFromLegacy(sourceLayout))
      : getProfileTopology(selectedProfile);
    setProfileName(selectedProfile.name);
    setTopology(nextTopology);
    setSelectedIdentityKey(identityKey(nextTopology.displays[0]?.identity));
    if (displayMode !== 'work') onDraftChange(selectedProfile.name, nextTopology);
    else if (!areTopologiesEqual(nextTopology, topologyFromLegacy(sourceLayout))) onWorkModeDraftChange?.(nextTopology);
    onValidationStateChange({ showNameError: false, showSelectionError: false });
  }, [displayMode, onDraftChange, onValidationStateChange, onWorkModeDraftChange, prefillFromSelectedProfile, prefillKey, selectedProfile, workModeLayout]);

  const selectedTarget = topology.displays.find((display) => identityKey(display.identity) === selectedIdentityKey) ?? topology.displays[0] ?? null;
  const topologyErrors = getTopologyEditorErrors(topology, t);
  const resetTopology = selectedProfile ? normalizeCanvasTopology(getProfileTopology(selectedProfile)) : { displays: [] };
  const canResetCanvas = !areTopologiesEqual(topology, resetTopology);
  const logicalDisplays = useMemo(() => deduplicatePhysicalDisplays(displays), [displays]);
  const availableDisplays = logicalDisplays.filter((display) => !topology.displays.some((target) => isSameProfileDisplay(target, { identity: display.identity, knownPhysicalDisplayId: display.knownPhysicalDisplayId })));

  useEffect(() => {
    let cancelled = false;
    const observed = selectedTarget ? logicalDisplays.find((display) => isSamePhysicalDisplay(display.identity, selectedTarget.identity)) : null;
    if (!selectedTarget || !observed || !observed.isEnabled) {
      setSelectedDisplayModes([]);
      return () => { cancelled = true; };
    }
    void sendBridgeCommand<DisplayModeQueryResult>('getDisplayModes', { identity: selectedTarget.identity }).then((result) => {
      if (!cancelled) setSelectedDisplayModes(result.modes.filter((mode) => mode.width === selectedTarget.width && mode.height === selectedTarget.height));
    }).catch(() => {
      if (!cancelled) setSelectedDisplayModes([]);
    });
    return () => { cancelled = true; };
  }, [logicalDisplays, selectedTarget?.identity, selectedTarget?.width, selectedTarget?.height]);

  function commitTopology(nextTopology: GamingProfileDisplayTopology) {
    setTopology(nextTopology);
    if (displayMode === 'work') onWorkModeDraftChange?.(nextTopology);
    else onDraftChange(profileName, nextTopology);
    setActionError(null);
  }

  function updateProfileName(value: string) {
    const normalized = normalizeGamingProfileName(value);
    setProfileName(normalized);
    if (displayMode === 'work') onWorkModeDraftChange?.(topology);
    else onDraftChange(normalized, topology);
    if (normalized && validationState.showNameError) onValidationStateChange({ ...validationState, showNameError: false });
  }

  function updateTarget(identity: ProfileDisplayTarget['identity'], patch: Partial<ProfileDisplayTarget>) {
    const nextDisplays = topology.displays.map((display) => ({
      ...display,
      ...(patch.isPrimary && !isSameProfileDisplay(display, { identity, knownPhysicalDisplayId: selectedTarget?.knownPhysicalDisplayId }) ? { isPrimary: false } : {}),
      ...(isSameProfileDisplay(display, { identity, knownPhysicalDisplayId: selectedTarget?.knownPhysicalDisplayId }) ? patch : {})
    }));
    commitTopology(normalizeTopologyPrimary({ displays: nextDisplays }));
  }

  async function addDisplay(display: DisplayDeviceInfo) {
    if (topology.displays.some((existing) => isSameProfileDisplay(existing, { identity: display.identity, knownPhysicalDisplayId: display.knownPhysicalDisplayId }))) {
      return;
    }
    const isFirstDisplay = topology.displays.length === 0;
    let modes: DisplayModeInfo[] = [];
    try {
      const result = await sendBridgeCommand<DisplayModeQueryResult>('getDisplayModes', { identity: display.identity });
      modes = result.modes;
    } catch {
      modes = [];
    }
    const target = profileTargetForAddition(display, modes, isFirstDisplay, topology.displays);
    const nextTopology = normalizeTopologyPrimary({ displays: [...topology.displays, target] });
    setSelectedIdentityKey(identityKey(display.identity));
    commitTopology(nextTopology);
  }

  async function useCurrentLayout() {
    try {
      setActionError(null);
      const current = await sendBridgeCommand<DisplayLayoutSnapshot>('captureDisplayLayout');
      const nextTopology = topologyFromLegacy(current);
      setSelectedIdentityKey(identityKey(nextTopology.displays[0]?.identity));
      commitTopology(nextTopology);
    } catch (reason: unknown) {
      setActionError(reason instanceof Error ? reason.message : t('profile.displayLoadError'));
    }
  }

  function resetCanvas() {
    const nextTopology = selectedProfile ? normalizeCanvasTopology(getProfileTopology(selectedProfile)) : { displays: [] };
    setSelectedIdentityKey(identityKey(nextTopology.displays[0]?.identity));
    commitTopology(nextTopology);
    onValidationStateChange({ showNameError: false, showSelectionError: false });
  }

  return (
    <div className="wizard-copy displays-step">
      <p className="eyebrow">{t('setup.step')} {stepNumber}</p>
      <h2>{displayMode === 'work' ? t('setup.workModeDisplays') : t('setup.displays')}</h2>
      <p>{displayMode === 'work' ? t('setup.defaultDisplayLayoutDescription') : t('setup.profileSetup')}</p>
      <section className="settings-section display-topology-editor">
        <div className="display-topology-toolbar display-profile-row">
          <div className="display-profile-field">
            <label className="profile-name-label" htmlFor="profile-name-input">{displayMode === 'work' ? t('setup.workModeDisplays') : t('profile.nameLabel')}</label>
            <div className="display-profile-input-row">
              <input id="profile-name-input" className="text-input profile-name-input" type="text" value={displayMode === 'work' ? t('setup.workMode') : profileName} readOnly={displayMode === 'work'} onChange={(event) => updateProfileName(event.target.value)} onBlur={() => onValidationStateChange({ ...validationState, showNameError: !profileName.trim() })} aria-invalid={displayMode !== 'work' && validationState.showNameError && !profileName.trim()} placeholder={t('profile.namePlaceholder')} />
              <button className="secondary-button compact display-reset-button" type="button" onClick={resetCanvas} disabled={!canResetCanvas} title={t('profile.resetDisplayCanvas')}>{t('profile.resetDisplayCanvas')}</button>
            </div>
          </div>
          <div className="display-diagnostics-actions">
            <button className="secondary-button compact" type="button" onClick={() => void useCurrentLayout()}>{t('profile.displayCurrentLayout')}</button>
            <button className="secondary-button compact" type="button" disabled={isRefreshingDisplays} onClick={refreshDisplays}>{isRefreshingDisplays ? t('profile.displayRefreshListBusy') : t('profile.displayRefreshList')}</button>
          </div>
        </div>
        {displayMode !== 'work' && validationState.showNameError && !profileName.trim() ? <div className="field-validation-error" role="alert">{t('profile.nameRequired')}</div> : null}
        {isDisplaySettling ? <div className="display-settling-status" role="status" aria-live="polite"><span className="display-settling-spinner" aria-hidden="true" /> <span>{t('profile.displaySynchronizing')}</span></div> : null}
        {displayListError ? <p className="error-line display-error">{displayListError}</p> : null}
        {actionError ? <p className="error-line display-error">{actionError}</p> : null}
        <div className={`display-topology-layout ${topology.displays.length === 0 ? 'is-empty' : ''}`}>
          <DisplayTopologyCanvas topology={topology} selectedIdentityKey={selectedIdentityKey} resolveDisplayName={(identity) => logicalDisplays.find((display) => isSamePhysicalDisplay(display.identity, identity))?.name || identity.friendlyName} onSelect={setSelectedIdentityKey} onMove={(identity, positionX, positionY) => updateTarget(identity, { positionX, positionY })} />
          <DisplayPropertiesPanel target={selectedTarget} observed={selectedTarget ? logicalDisplays.find((display) => isSameProfileDisplay(selectedTarget, { identity: display.identity, knownPhysicalDisplayId: display.knownPhysicalDisplayId })) ?? null : null} modes={selectedDisplayModes} onChange={(patch) => selectedTarget && updateTarget(selectedTarget.identity, patch)} onRemove={() => selectedTarget && commitTopology({ displays: topology.displays.filter((display) => !isSameProfileDisplay(display, selectedTarget)) })} />
        </div>
        {topology.displays.length > 0 ? <DisplayTopologyValidation errors={topologyErrors} showSelectionError={validationState.showSelectionError} /> : null}
        <section className="display-inventory" aria-label={t('profile.displayHeading')}>
          <div className="display-module-header">
            <div className="display-inventory-header">
              <strong>{t('profile.displayHeading')}</strong>
              <span className="display-inventory-count">{availableDisplays.length}</span>
            </div>
            <p>{t('profile.displayClickHint')}</p>
          </div>
          <div className="display-inventory-list">
            {availableDisplays.map((display) => <button className="display-inventory-item" type="button" key={identityKey(display.identity)} onClick={() => void addDisplay(display)}><span className="display-inventory-icon">+</span><strong>{display.name || t('profile.displayUnknown')}</strong><span>{formatDisplayResolution(display)} · {display.isEnabled ? t('profile.displayEnabled') : t('profile.displayDisabled')}</span>{display.catalogState === 'Unavailable' ? <span className="display-inventory-detail">{t('profile.displayStateUnavailable')}</span> : null}<em>{t('profile.addDisplay')}</em></button>)}
            {availableDisplays.length === 0 ? <p className="hint-line">{t('profile.emptyState')}</p> : null}
          </div>
        </section>
      </section>
    </div>
  );
}

function DisplayTopologyCanvas({ topology, selectedIdentityKey, resolveDisplayName, onSelect, onMove }: { topology: GamingProfileDisplayTopology; selectedIdentityKey: string | null; resolveDisplayName: (identity: ProfileDisplayTarget['identity']) => string; onSelect: (key: string) => void; onMove: (identity: ProfileDisplayTarget['identity'], positionX: number, positionY: number) => void }) {
  const { t } = useTranslation();
  const dragRef = useRef<DragState | null>(null);
  const canvasRef = useRef<HTMLDivElement | null>(null);
  const [viewport, setViewport] = useState({ width: 760, height: 320 });
  const [dragPreview, setDragPreview] = useState<{ identityKey: string; rect: GeometryRect } | null>(null);
  const rects = topology.displays.map(display => rectFromPosition(display.positionX, display.positionY, display.width, display.height));
  const bounds = getBounds(topology.displays);
  const largestDisplayDimension = rects.reduce((largest, rect) => Math.max(largest, rect.width, rect.height), 0);
  const fixedCanvasHeight = 360;
  const transform = createCanvasTransform(bounds, viewport.width, viewport.height, 24, largestDisplayDimension);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const updateViewport = () => setViewport({ width: canvas.clientWidth, height: fixedCanvasHeight });
    updateViewport();
    const observer = new ResizeObserver(updateViewport);
    observer.observe(canvas);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    function handlePointerMove(event: PointerEvent) {
      const drag = dragRef.current;
      if (!drag) return;
      const target = topology.displays.find((display) => identityKey(display.identity) === drag.identityKey);
      if (!target) return;
      const deltaX = event.clientX - drag.startX;
      const deltaY = event.clientY - drag.startY;
      if (!drag.hasMoved && Math.hypot(deltaX, deltaY) < 5) return;
      drag.hasMoved = true;
      const desired = rectFromPosition(drag.startRect.left + deltaX / drag.transform.scale, drag.startRect.top + deltaY / drag.transform.scale, drag.startRect.width, drag.startRect.height);
      const otherRects = drag.topologyRects.filter((_, index) => topology.displays[index] && identityKey(topology.displays[index].identity) !== drag.identityKey);
      const resolved = snapAndResolve(desired, otherRects);
      drag.currentRect = resolved;
      setDragPreview({ identityKey: drag.identityKey, rect: resolved });
    }

    function finishDrag(commit: boolean) {
      const drag = dragRef.current;
      if (commit && drag?.hasMoved) {
        const target = topology.displays.find((display) => identityKey(display.identity) === drag.identityKey);
        if (target) onMove(target.identity, Math.round(drag.currentRect.left), Math.round(drag.currentRect.top));
      }
      dragRef.current = null;
      setDragPreview(null);
    }

    function handlePointerUp() {
      finishDrag(true);
    }

    function handlePointerCancel() {
      finishDrag(false);
    }
    window.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointerup', handlePointerUp);
    window.addEventListener('pointercancel', handlePointerCancel);
    return () => {
      window.removeEventListener('pointermove', handlePointerMove);
      window.removeEventListener('pointerup', handlePointerUp);
      window.removeEventListener('pointercancel', handlePointerCancel);
    };
  }, [onMove, topology.displays]);

  function beginDrag(event: React.PointerEvent, identity: ProfileDisplayTarget['identity'], index: number) {
    if (event.button !== 0) return;
    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
    onSelect(identityKey(identity));
    dragRef.current = { identityKey: identityKey(identity), startX: event.clientX, startY: event.clientY, startRect: rects[index], currentRect: rects[index], transform, topologyRects: rects, hasMoved: false };
  }

  const previewRect = dragPreview ? rectToCanvas(dragPreview.rect, transform) : null;
  const dockingGuides = dragPreview ? getDockingGuides(dragPreview.rect, rects.filter((_, index) => topology.displays[index] && identityKey(topology.displays[index].identity) !== dragPreview.identityKey)) : [];
  return <div ref={canvasRef} className={`display-topology-canvas ${topology.displays.length === 0 ? 'is-empty' : ''}`} style={{ height: `${fixedCanvasHeight}px` }} aria-label={t('profile.topologyPreview')}>
    {topology.displays.map((display, index) => {
      const key = identityKey(display.identity);
      const topologyRect = dragPreview?.identityKey === key ? dragPreview.rect : rects[index];
      const visualRect = rectToCanvas(topologyRect, transform);
      return <button className={`display-topology-node ${display.enabled ? '' : 'is-disabled'} ${display.isPrimary ? 'is-primary' : ''} ${selectedIdentityKey === key ? 'is-selected' : ''} ${dragPreview?.identityKey === key ? 'is-dragging' : ''}`} style={{ left: `${visualRect.left}px`, top: `${visualRect.top}px`, width: `${visualRect.width}px`, height: `${visualRect.height}px` }} type="button" key={key} onClick={() => onSelect(key)} onPointerDown={(event) => beginDrag(event, display.identity, index)}>
        <span className="display-topology-node-number">{index + 1}</span><strong>{resolveDisplayName(display.identity) || t('profile.displayUnknown')}</strong><small>{display.width > 0 ? `${display.width} × ${display.height}` : t('profile.topologyUnknownMode')}</small>{display.isPrimary ? <em>{t('profile.topologyPrimary')}</em> : null}
      </button>;
    })}
    {previewRect ? <><div className="display-topology-preview" aria-hidden="true" style={{ left: `${previewRect.left}px`, top: `${previewRect.top}px`, width: `${previewRect.width}px`, height: `${previewRect.height}px` }} />{dockingGuides.map((guide, index) => guide.orientation === 'vertical'
      ? <div className="display-topology-guide is-vertical" aria-hidden="true" key={`vertical-${index}`} style={{ left: `${guide.position * transform.scale + transform.offsetX}px`, top: `${guide.start * transform.scale + transform.offsetY}px`, height: `${guide.length * transform.scale}px` }} />
      : <div className="display-topology-guide is-horizontal" aria-hidden="true" key={`horizontal-${index}`} style={{ left: `${guide.start * transform.scale + transform.offsetX}px`, top: `${guide.position * transform.scale + transform.offsetY}px`, width: `${guide.length * transform.scale}px` }} />)}</> : null}
    {!dragPreview && topology.displays.length > 1 ? <span className="display-topology-canvas-hint">{t('profile.topologyDragHint')}</span> : null}
    {topology.displays.length === 0 ? <p className="display-topology-empty">{t('profile.topologyEmpty')}</p> : null}
  </div>;
}


function DisplayPropertiesPanel({ target, observed, modes, onChange, onRemove }: { target: ProfileDisplayTarget | null; observed: DisplayDeviceInfo | null; modes: DisplayModeInfo[]; onChange: (patch: Partial<ProfileDisplayTarget>) => void; onRemove: () => void }) {
  const { t } = useTranslation();
  if (!target) return <aside className="display-properties-panel is-empty"><strong>{t('profile.displayNoSelection')}</strong><span>{t('profile.displayNoSelectionHint')}</span></aside>;
  const refreshState = getRefreshSelectionState(target, observed, modes);
  const refreshOptions = refreshState.modes;
  const isAutoHighest = getRefreshPolicy(target) === 'AutoHighest';
  const refreshPlaceholder = !observed
    ? t('profile.refreshNoData')
    : refreshState.unavailable
      ? t('profile.refreshUnavailable')
      : refreshOptions.length === 0
        ? t('profile.refreshPick')
        : undefined;
  return <aside className="display-properties-panel" aria-label={t('profile.displayProperties')}>
    <div className="display-properties-title"><div><strong>{observed?.name || target.identity.friendlyName || t('profile.displayUnknown')}</strong><span>{observed?.connectionTechnology || t('profile.displayConnectionUnknown')}</span></div><button className="text-action" type="button" onClick={onRemove}>{t('profile.removeDisplay')}</button></div>
    {observed?.catalogState === 'Unavailable' ? <p className="hint-line">{t('profile.displayStateUnavailable')}</p> : null}
    <div className="display-property-section"><span className="display-property-heading">{t('profile.displayStatus')}</span><label className="toggle-row"><span>{t('profile.displayUsed')}</span><input type="checkbox" checked={target.enabled} onChange={(event) => onChange({ enabled: event.target.checked, isPrimary: event.target.checked ? target.isPrimary : false })} /></label></div>
    <div className="display-property-section"><span className="display-property-heading">{t('profile.displaySettings')}</span><label className="toggle-row"><span>{t('profile.displayPrimary')}</span><input type="checkbox" checked={target.isPrimary} disabled={!target.enabled} onChange={(event) => onChange({ isPrimary: event.target.checked })} /></label><div className="display-properties-grid"><label>{t('profile.positionX')}<input className="text-input" type="number" value={target.positionX} onFocus={(event) => event.target.select()} onChange={(event) => onChange({ positionX: Number(event.target.value) })} /></label><label>{t('profile.positionY')}<input className="text-input" type="number" value={target.positionY} onFocus={(event) => event.target.select()} onChange={(event) => onChange({ positionY: Number(event.target.value) })} /></label><label>{t('profile.width')}<input className="text-input" type="number" min="1" value={target.width} onFocus={(event) => event.target.select()} onChange={(event) => onChange({ width: Number(event.target.value), fixedRefreshRateNumerator: 0, fixedRefreshRateDenominator: 0, refreshRateNumerator: 0, refreshRateDenominator: 0 })} /></label><label>{t('profile.height')}<input className="text-input" type="number" min="1" value={target.height} onFocus={(event) => event.target.select()} onChange={(event) => onChange({ height: Number(event.target.value), fixedRefreshRateNumerator: 0, fixedRefreshRateDenominator: 0, refreshRateNumerator: 0, refreshRateDenominator: 0 })} /></label></div></div>
    <div className="display-property-section"><span className="display-property-heading">{t('profile.refresh')}</span><div className="display-refresh-field"><div className="display-refresh-select"><select className="text-input" value={resolveRefreshSelectionKey(target, refreshOptions)} disabled={!isAutoHighest && refreshState.disabled} onChange={(event) => { if (event.target.value === 'auto') { onChange({ refreshPolicy: 'AutoHighest', fixedRefreshRateNumerator: 0, fixedRefreshRateDenominator: 0, refreshRateNumerator: 0, refreshRateDenominator: 0 }); return; } const mode = refreshOptions.find((candidate) => modeKey(candidate) === event.target.value); if (mode) onChange({ refreshPolicy: 'Fixed', fixedRefreshRateNumerator: mode.refreshRateNumerator, fixedRefreshRateDenominator: mode.refreshRateDenominator, refreshRateNumerator: 0, refreshRateDenominator: 0 }); }}><option value="auto">{t('profile.refreshAuto')}</option>{refreshOptions.map((mode) => <option key={modeKey(mode)} value={modeKey(mode)}>{formatRefresh(mode.refreshRateHz)}</option>)}{!isAutoHighest && refreshOptions.length === 0 && hasValidFixedRefresh(target) ? <option value={fixedModeKey(target)}>{formatRefresh((target.fixedRefreshRateNumerator ?? 0) / (target.fixedRefreshRateDenominator ?? 1))}</option> : null}{refreshOptions.length === 0 && !hasValidFixedRefresh(target) && !isAutoHighest ? <option value="" disabled>{refreshPlaceholder}</option> : null}</select><span className="display-refresh-chevron" aria-hidden="true" /></div></div>{observed && !observed.isEnabled && getRefreshPolicy(target) === 'Fixed' && hasValidFixedRefresh(target) ? <p className="hint-line">{t('profile.refreshSavedHint')}</p> : null}{!observed ? <p className="hint-line">{t('profile.refreshMissingHint')}</p> : null}</div>
  </aside>;
}

function modeKey(mode: Pick<ProfileDisplayTarget, 'refreshRateNumerator' | 'refreshRateDenominator'>) {
  return `${mode.refreshRateNumerator}/${mode.refreshRateDenominator}`;
}

function fixedModeKey(target: Pick<ProfileDisplayTarget, 'fixedRefreshRateNumerator' | 'fixedRefreshRateDenominator'>) {
  return `${target.fixedRefreshRateNumerator ?? 0}/${target.fixedRefreshRateDenominator ?? 0}`;
}

function resolveRefreshSelectionKey(target: ProfileDisplayTarget, modes: DisplayModeInfo[]) {
  if (getRefreshPolicy(target) === 'AutoHighest') return 'auto';
  const fixedMode = modes.find((candidate) => Math.abs(candidate.refreshRateHz - (target.fixedRefreshRateNumerator ?? 0) / (target.fixedRefreshRateDenominator || 1)) <= 0.25);
  return fixedMode ? modeKey(fixedMode) : fixedModeKey(target);
}

function formatRefresh(refreshRate: number) {
  return Number.isInteger(refreshRate) ? `${refreshRate} Hz` : `${refreshRate.toFixed(2)} Hz`;
}

function DisplayTopologyValidation({ errors, showSelectionError }: { errors: string[]; showSelectionError: boolean }) {
  const { t } = useTranslation();
  if (!showSelectionError && errors.length === 0) return null;
  const fallbackMissing = t('profile.validationMinDisplays');
  return <div className="display-topology-validation" role="alert">{(showSelectionError ? [fallbackMissing, ...errors] : errors).filter((error, index, all) => all.indexOf(error) === index).map((error) => <span key={error}>{error}</span>)}</div>;
}

function identityKey(identity: ProfileDisplayTarget['identity'] | undefined): string {
  if (!identity) return '';
  if (identity.adapterLuid !== 0 && identity.targetId !== 0) return `target:${identity.adapterLuid}:${identity.targetId}`;
  const monitorDevicePath = identity.monitorDevicePath?.trim();
  if (monitorDevicePath) return `path:${monitorDevicePath.toLowerCase()}`;

  return [
    identity.connectorInstance,
    identity.sourceId,
    identity.outputTechnology,
    identity.edidIdsValid ? identity.edidManufacturerId : 0,
    identity.edidIdsValid ? identity.edidProductCodeId : 0
  ].map(value => String(value ?? '').trim().toLowerCase()).join(':');
}

function getBounds(displays: ProfileDisplayTarget[]) {
  return computeTopologyBounds(displays.map(display => rectFromPosition(display.positionX, display.positionY, display.width, display.height)));
}
