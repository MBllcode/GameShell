import { useEffect, useMemo, useRef, useState } from 'react';
import type { DisplayDeviceInfo, DisplayLayoutProfile, DisplayLayoutSnapshot, DisplayModeInfo, DisplayModeQueryResult, GamingProfileDisplayTopology, ProfileDisplayTarget } from '../types/bridge';
import { normalizePcGamingProfileName } from '../features/profiles/utils';
import { computeTopologyBounds, createCanvasTransform, rectFromPosition, rectToCanvas, snapAndResolve, type CanvasTransform, type GeometryRect } from '../utils/displayTopologyGeometry';
import { getProfileTopology, getRefreshPolicy, getRefreshSelectionState, getTopologyEditorErrors, hasValidFixedRefresh, normalizeTopologyPrimary, profileTargetForAddition, sameIdentity, topologyFromLegacy } from '../utils/displays';
import { sendBridgeCommand } from '../services/webViewBridge';

type DisplayValidationState = { showNameError: boolean; showSelectionError: boolean };
type DragState = { identityKey: string; startX: number; startY: number; startRect: GeometryRect; transform: CanvasTransform; topologyRects: GeometryRect[] };

type DisplaysStepProps = {
  stepNumber: number;
  displays: DisplayDeviceInfo[];
  pcGamingProfiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  prefillFromSelectedProfile: boolean;
  onDraftChange: (profileName: string, topology: GamingProfileDisplayTopology) => void;
  validationState: DisplayValidationState;
  onValidationStateChange: (state: DisplayValidationState) => void;
  isRefreshingDisplays: boolean;
  displayListError: string | null;
  refreshDisplays: () => Promise<void>;
};

export function DisplaysStep({
  stepNumber,
  displays,
  pcGamingProfiles,
  selectedProfileName,
  prefillFromSelectedProfile,
  onDraftChange,
  validationState,
  onValidationStateChange,
  isRefreshingDisplays,
  displayListError,
  refreshDisplays
}: DisplaysStepProps) {
  const selectedProfile = pcGamingProfiles.find((profile) => profile.name === selectedProfileName) ?? null;
  const [profileName, setProfileName] = useState('');
  const [topology, setTopology] = useState<GamingProfileDisplayTopology>({ displays: [] });
  const [selectedIdentityKey, setSelectedIdentityKey] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [selectedDisplayModes, setSelectedDisplayModes] = useState<DisplayModeInfo[]>([]);

  useEffect(() => {
    if (!prefillFromSelectedProfile || !selectedProfile) return;
    const nextTopology = getProfileTopology(selectedProfile);
    setProfileName(selectedProfile.name);
    setTopology(nextTopology);
    setSelectedIdentityKey(identityKey(nextTopology.displays[0]?.identity));
    onDraftChange(selectedProfile.name, nextTopology);
    onValidationStateChange({ showNameError: false, showSelectionError: false });
  }, [onDraftChange, onValidationStateChange, prefillFromSelectedProfile, selectedProfile]);

  const selectedTarget = topology.displays.find((display) => identityKey(display.identity) === selectedIdentityKey) ?? topology.displays[0] ?? null;
  const topologyErrors = getTopologyEditorErrors(topology);
  const savedIdentityKeys = useMemo(() => new Set(topology.displays.map((display) => identityKey(display.identity))), [topology.displays]);
  const availableDisplays = displays.filter((display) => !savedIdentityKeys.has(identityKey(display.identity)));

  useEffect(() => {
    let cancelled = false;
    const observed = selectedTarget ? displays.find((display) => sameIdentity(display.identity, selectedTarget.identity)) : null;
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
  }, [displays, selectedTarget?.identity, selectedTarget?.width, selectedTarget?.height]);

  function commitTopology(nextTopology: GamingProfileDisplayTopology) {
    setTopology(nextTopology);
    onDraftChange(profileName, nextTopology);
    setActionError(null);
  }

  function updateProfileName(value: string) {
    const normalized = normalizePcGamingProfileName(value);
    setProfileName(normalized);
    onDraftChange(normalized, topology);
    if (normalized && validationState.showNameError) onValidationStateChange({ ...validationState, showNameError: false });
  }

  function updateTarget(identity: ProfileDisplayTarget['identity'], patch: Partial<ProfileDisplayTarget>) {
    const nextDisplays = topology.displays.map((display) => ({
      ...display,
      ...(patch.isPrimary && !sameIdentity(display.identity, identity) ? { isPrimary: false } : {}),
      ...(sameIdentity(display.identity, identity) ? patch : {})
    }));
    commitTopology(normalizeTopologyPrimary({ displays: nextDisplays }));
  }

  async function addDisplay(display: DisplayDeviceInfo) {
    const isFirstDisplay = topology.displays.length === 0;
    let modes: DisplayModeInfo[] = [];
    try {
      const result = await sendBridgeCommand<DisplayModeQueryResult>('getDisplayModes', { identity: display.identity });
      modes = result.modes;
    } catch {
      modes = [];
    }
    const target = profileTargetForAddition(display, modes, isFirstDisplay);
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
      setActionError(reason instanceof Error ? reason.message : 'Nie udało się pobrać bieżącego układu ekranów.');
    }
  }

  return (
    <div className="wizard-copy displays-step">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Ekrany</h2>
      <section className="settings-section display-topology-editor">
        <div className="display-topology-toolbar display-profile-row">
          <div className="display-profile-field">
            <label className="profile-name-label" htmlFor="profile-name-input">Nazwa profilu</label>
            <input id="profile-name-input" className="text-input profile-name-input" type="text" value={profileName} onChange={(event) => updateProfileName(event.target.value)} onBlur={() => onValidationStateChange({ ...validationState, showNameError: !profileName.trim() })} aria-invalid={validationState.showNameError && !profileName.trim()} placeholder="np. Salon TV + biurko" />
          </div>
          <div className="display-diagnostics-actions">
            <button className="secondary-button compact" type="button" onClick={() => void useCurrentLayout()}>Użyj bieżącego układu</button>
            <button className="secondary-button compact" type="button" disabled={isRefreshingDisplays} onClick={refreshDisplays}>{isRefreshingDisplays ? 'Odświeżanie...' : 'Odśwież listę'}</button>
          </div>
        </div>
        {validationState.showNameError && !profileName.trim() ? <div className="field-validation-error" role="alert">Wpisz nazwę profilu.</div> : null}
        {displayListError ? <p className="error-line display-error">{displayListError}</p> : null}
        {actionError ? <p className="error-line display-error">{actionError}</p> : null}
        <div className={`display-topology-layout ${topology.displays.length === 0 ? 'is-empty' : ''}`}>
          <DisplayTopologyCanvas topology={topology} selectedIdentityKey={selectedIdentityKey} onSelect={setSelectedIdentityKey} onMove={(identity, positionX, positionY) => updateTarget(identity, { positionX, positionY })} />
          <DisplayPropertiesPanel target={selectedTarget} observed={selectedTarget ? displays.find((display) => sameIdentity(display.identity, selectedTarget.identity)) ?? null : null} modes={selectedDisplayModes} onChange={(patch) => selectedTarget && updateTarget(selectedTarget.identity, patch)} onRemove={() => selectedTarget && commitTopology({ displays: topology.displays.filter((display) => !sameIdentity(display.identity, selectedTarget.identity)) })} />
        </div>
        {topology.displays.length > 0 ? <DisplayTopologyValidation errors={topologyErrors} showSelectionError={validationState.showSelectionError} /> : null}
        <section className="display-inventory" aria-label="Wykryte ekrany dostępne do dodania">
          <div className="display-module-header">
            <div className="display-inventory-header">
              <strong>Wykryte ekrany</strong>
              <span className="display-inventory-count">{availableDisplays.length}</span>
            </div>
            <p>Kliknij kafelek, aby dodać ekran do układu profilu.</p>
          </div>
          <div className="display-inventory-list">
            {availableDisplays.map((display) => <button className="display-inventory-item" type="button" key={identityKey(display.identity)} onClick={() => void addDisplay(display)}><span className="display-inventory-icon">+</span><strong>{display.name || 'Nieznany ekran'}</strong><span>{display.width || display.preferredWidth} × {display.height || display.preferredHeight} · {display.isEnabled ? 'aktywny' : 'nieaktywny'}</span><em>Dodaj do układu</em></button>)}
            {availableDisplays.length === 0 ? <p className="hint-line">Brak dodatkowych wykrytych ekranów.</p> : null}
          </div>
        </section>
      </section>
    </div>
  );
}

function DisplayTopologyCanvas({ topology, selectedIdentityKey, onSelect, onMove }: { topology: GamingProfileDisplayTopology; selectedIdentityKey: string | null; onSelect: (key: string) => void; onMove: (identity: ProfileDisplayTarget['identity'], positionX: number, positionY: number) => void }) {
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
      const desired = rectFromPosition(drag.startRect.left + (event.clientX - drag.startX) / drag.transform.scale, drag.startRect.top + (event.clientY - drag.startY) / drag.transform.scale, drag.startRect.width, drag.startRect.height);
      const otherRects = drag.topologyRects.filter((_, index) => topology.displays[index] && identityKey(topology.displays[index].identity) !== drag.identityKey);
      const resolved = snapAndResolve(desired, otherRects);
      setDragPreview({ identityKey: drag.identityKey, rect: resolved });
      onMove(target.identity, Math.round(resolved.left), Math.round(resolved.top));
    }
    function handlePointerUp() { dragRef.current = null; setDragPreview(null); }
    window.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointerup', handlePointerUp);
    return () => { window.removeEventListener('pointermove', handlePointerMove); window.removeEventListener('pointerup', handlePointerUp); };
  }, [onMove, topology.displays]);

  function beginDrag(event: React.PointerEvent, identity: ProfileDisplayTarget['identity'], index: number) {
    if (event.button !== 0) return;
    dragRef.current = { identityKey: identityKey(identity), startX: event.clientX, startY: event.clientY, startRect: rects[index], transform, topologyRects: rects };
  }

  const previewRect = dragPreview ? rectToCanvas(dragPreview.rect, transform) : null;
  return <div ref={canvasRef} className={`display-topology-canvas ${topology.displays.length === 0 ? 'is-empty' : ''}`} style={{ height: `${fixedCanvasHeight}px` }} aria-label="Podgląd układu ekranów">
    {topology.displays.map((display, index) => {
      const key = identityKey(display.identity);
      const topologyRect = dragPreview?.identityKey === key ? dragPreview.rect : rects[index];
      const visualRect = rectToCanvas(topologyRect, transform);
      return <button className={`display-topology-node ${display.enabled ? '' : 'is-disabled'} ${display.isPrimary ? 'is-primary' : ''} ${selectedIdentityKey === key ? 'is-selected' : ''} ${dragPreview?.identityKey === key ? 'is-dragging' : ''}`} style={{ left: `${visualRect.left}px`, top: `${visualRect.top}px`, width: `${visualRect.width}px`, height: `${visualRect.height}px` }} type="button" key={key} onClick={() => onSelect(key)} onPointerDown={(event) => beginDrag(event, display.identity, index)}>
        <span className="display-topology-node-number">{index + 1}</span><strong>{display.identity.friendlyName || 'Ekran'}</strong><small>{display.width > 0 ? `${display.width} × ${display.height}` : 'Tryb nieznany'}</small>{display.isPrimary ? <em>GŁÓWNY</em> : null}
      </button>;
    })}
    {previewRect ? <><div className="display-topology-preview" aria-hidden="true" style={{ left: `${previewRect.left}px`, top: `${previewRect.top}px`, width: `${previewRect.width}px`, height: `${previewRect.height}px` }} /><div className="display-topology-guide is-vertical" aria-hidden="true" style={{ left: `${previewRect.left}px` }} /><div className="display-topology-guide is-horizontal" aria-hidden="true" style={{ top: `${previewRect.top}px` }} /></> : null}
    {topology.displays.length === 0 ? <p className="display-topology-empty">Dodaj ekran poniżej albo użyj bieżącego układu.</p> : null}
  </div>;
}

/* Obsolete fragment retained only as patch context until the replacement below. */
/*
  const refreshOptions = refreshState.modes;
  const isAutoHighest = getRefreshPolicy(target) === 'AutoHighest';
  const refreshPlaceholder = !observed
    ? 'Brak danych bieżącego ekranu'
    : refreshState.unavailable
      ? 'Brak danych — włącz ekran, aby wykryć częstotliwości'
        : refreshOptions.length === 0
          ? 'Wybierz częstotliwość'
          : undefined;
    return (
      <aside className="display-properties-panel" aria-label="Właściwości wybranego ekranu">
        <div className="display-properties-title"><div><strong>{target.identity.friendlyName || 'Ekran'}</strong><span>{observed?.connectionTechnology || 'Brak bieżących danych połączenia'}</span></div><button className="text-action" type="button" onClick={onRemove}>Usuń</button></div>
        <div className="display-property-section">
          <span className="display-property-heading">Stan</span>
          <label className="toggle-row"><span>Włączony w profilu</span><input type="checkbox" checked={target.enabled} onChange={(event) => onChange({ enabled: event.target.checked, isPrimary: event.target.checked ? target.isPrimary : false })} /></label>
        </div>
        <div className="display-property-section">
          <span className="display-property-heading">Główne ustawienia</span>
          <label className="toggle-row"><span>Główny ekran</span><input type="checkbox" checked={target.isPrimary} disabled={!target.enabled} onChange={(event) => onChange({ isPrimary: event.target.checked })} /></label>
          <div className="display-properties-grid">
            <label>Pozycja X<input className="text-input" type="number" value={target.positionX} onChange={(event) => onChange({ positionX: Number(event.target.value) })} /></label>
            <label>Pozycja Y<input className="text-input" type="number" value={target.positionY} onChange={(event) => onChange({ positionY: Number(event.target.value) })} /></label>
            <label>Szerokość<input className="text-input" type="number" min="1" value={target.width} onChange={(event) => onChange({ width: Number(event.target.value), fixedRefreshRateNumerator: 0, fixedRefreshRateDenominator: 0, refreshRateNumerator: 0, refreshRateDenominator: 0 })} /></label>
            <label>Wysokość<input className="text-input" type="number" min="1" value={target.height} onChange={(event) => onChange({ height: Number(event.target.value), fixedRefreshRateNumerator: 0, fixedRefreshRateDenominator: 0, refreshRateNumerator: 0, refreshRateDenominator: 0 })} /></label>
          </div>
        </div>
        <div className="display-property-section">
          <span className="display-property-heading">Odświeżanie</span>
          <div className="display-refresh-field"><div className="display-refresh-select">
            <select className="text-input" value={resolveRefreshSelectionKey(target, refreshOptions)} disabled={!isAutoHighest && refreshState.disabled} onChange={(event) => { if (event.target.value === 'auto') { onChange({ refreshPolicy: 'AutoHighest', fixedRefreshRateNumerator: 0, fixedRefreshRateDenominator: 0, refreshRateNumerator: 0, refreshRateDenominator: 0 }); return; } const mode = refreshOptions.find((candidate) => modeKey(candidate) === event.target.value); if (mode) onChange({ refreshPolicy: 'Fixed', fixedRefreshRateNumerator: mode.refreshRateNumerator, fixedRefreshRateDenominator: mode.refreshRateDenominator, refreshRateNumerator: 0, refreshRateDenominator: 0 }); }}>
              <option value="auto">Najwyższe dostępne</option>
              {refreshOptions.map((mode) => <option key={modeKey(mode)} value={modeKey(mode)}>{formatRefresh(mode.refreshRateHz)}</option>)}
              {!isAutoHighest && refreshOptions.length === 0 && hasValidFixedRefresh(target) ? <option value={fixedModeKey(target)}>{formatRefresh((target.fixedRefreshRateNumerator ?? 0) / (target.fixedRefreshRateDenominator ?? 1))}</option> : null}
              {refreshOptions.length === 0 && !hasValidFixedRefresh(target) && !isAutoHighest ? <option value="" disabled>{refreshPlaceholder}</option> : null}
            </select>
            <span className="display-refresh-chevron" aria-hidden="true" />
          </div></div>
          {observed && !observed.isEnabled && getRefreshPolicy(target) === 'Fixed' && hasValidFixedRefresh(target) ? <p className="hint-line">Pokazano zapisaną stałą częstotliwość profilu. Pełną listę trybów można zweryfikować po włączeniu ekranu.</p> : null}
          {!observed ? <p className="hint-line">Ekran nie jest obecnie wykryty. Zapisana konfiguracja pozostaje bez zmian.</p> : null}
        </div>
      </aside>
    );
    /*
    <div className="display-properties-title"><div><strong>{target.identity.friendlyName || 'Ekran'}</strong><span>{observed?.connectionTechnology || 'Brak bieżących danych połączenia'}</span></div><button className="text-action" type="button" onClick={onRemove}>Usuń</button></div>
    <label className="toggle-row"><span>Włączony w profilu</span><input type="checkbox" checked={target.enabled} onChange={(event) => onChange({ enabled: event.target.checked, isPrimary: event.target.checked ? target.isPrimary : false })} /></label>
    <label className="toggle-row"><span>Główny ekran</span><input type="checkbox" checked={target.isPrimary} disabled={!target.enabled} onChange={(event) => onChange({ isPrimary: event.target.checked })} /></label>
    <div className="display-properties-grid"><label>Pozycja X<input className="text-input" type="number" value={target.positionX} onChange={(event) => onChange({ positionX: Number(event.target.value) })} /></label><label>Pozycja Y<input className="text-input" type="number" value={target.positionY} onChange={(event) => onChange({ positionY: Number(event.target.value) })} /></label><label>Szerokość<input className="text-input" type="number" min="1" value={target.width} onChange={(event) => onChange({ width: Number(event.target.value), fixedRefreshRateNumerator: 0, fixedRefreshRateDenominator: 0, refreshRateNumerator: 0, refreshRateDenominator: 0 })} /></label><label>Wysokość<input className="text-input" type="number" min="1" value={target.height} onChange={(event) => onChange({ height: Number(event.target.value), fixedRefreshRateNumerator: 0, fixedRefreshRateDenominator: 0, refreshRateNumerator: 0, refreshRateDenominator: 0 })} /></label><label className="display-refresh-field">Odświeżanie<div className="display-refresh-select"><select className="text-input" value={resolveRefreshSelectionKey(target, refreshOptions)} disabled={!isAutoHighest && refreshState.disabled} onChange={(event) => { if (event.target.value === 'auto') { onChange({ refreshPolicy: 'AutoHighest', fixedRefreshRateNumerator: 0, fixedRefreshRateDenominator: 0, refreshRateNumerator: 0, refreshRateDenominator: 0 }); return; } const mode = refreshOptions.find((candidate) => modeKey(candidate) === event.target.value); if (mode) onChange({ refreshPolicy: 'Fixed', fixedRefreshRateNumerator: mode.refreshRateNumerator, fixedRefreshRateDenominator: mode.refreshRateDenominator, refreshRateNumerator: 0, refreshRateDenominator: 0 }); }}><option value="auto">Najwyższe dostępne</option>{refreshOptions.map((mode) => <option key={modeKey(mode)} value={modeKey(mode)}>{formatRefresh(mode.refreshRateHz)}</option>)}{!isAutoHighest && refreshOptions.length === 0 && hasValidFixedRefresh(target) ? <option value={fixedModeKey(target)}>{formatRefresh((target.fixedRefreshRateNumerator ?? 0) / (target.fixedRefreshRateDenominator ?? 1))}</option> : null}{refreshOptions.length === 0 && !hasValidFixedRefresh(target) && !isAutoHighest ? <option value="" disabled>{refreshPlaceholder}</option> : null}</select><span className="display-refresh-chevron" aria-hidden="true" /></div></label></div>
    {observed && !observed.isEnabled && getRefreshPolicy(target) === 'Fixed' && hasValidFixedRefresh(target) ? <p className="hint-line">Pokazano zapisaną stałą częstotliwość profilu. Pełną listę trybów można zweryfikować po włączeniu ekranu.</p> : null}
    {!observed ? <p className="hint-line">Ekran nie jest obecnie wykryty. Zapisana konfiguracja pozostaje bez zmian.</p> : null}
  </aside>;
  */

function DisplayPropertiesPanel({ target, observed, modes, onChange, onRemove }: { target: ProfileDisplayTarget | null; observed: DisplayDeviceInfo | null; modes: DisplayModeInfo[]; onChange: (patch: Partial<ProfileDisplayTarget>) => void; onRemove: () => void }) {
  if (!target) return <aside className="display-properties-panel is-empty"><strong>Wybierz ekran</strong><span>Jego ustawienia pojawią się tutaj.</span></aside>;
  const refreshState = getRefreshSelectionState(target, observed, modes);
  const refreshOptions = refreshState.modes;
  const isAutoHighest = getRefreshPolicy(target) === 'AutoHighest';
  const refreshPlaceholder = !observed
    ? 'Brak danych bieżącego ekranu'
    : refreshState.unavailable
      ? 'Brak danych — włącz ekran, aby wykryć częstotliwości'
      : refreshOptions.length === 0
        ? 'Wybierz częstotliwość'
        : undefined;
  return <aside className="display-properties-panel" aria-label="Właściwości wybranego ekranu">
    <div className="display-properties-title"><div><strong>{target.identity.friendlyName || 'Ekran'}</strong><span>{observed?.connectionTechnology || 'Brak bieżących danych połączenia'}</span></div><button className="text-action" type="button" onClick={onRemove}>Usuń</button></div>
    <div className="display-property-section"><span className="display-property-heading">Stan</span><label className="toggle-row"><span>Włączony w profilu</span><input type="checkbox" checked={target.enabled} onChange={(event) => onChange({ enabled: event.target.checked, isPrimary: event.target.checked ? target.isPrimary : false })} /></label></div>
    <div className="display-property-section"><span className="display-property-heading">Główne ustawienia</span><label className="toggle-row"><span>Główny ekran</span><input type="checkbox" checked={target.isPrimary} disabled={!target.enabled} onChange={(event) => onChange({ isPrimary: event.target.checked })} /></label><div className="display-properties-grid"><label>Pozycja X<input className="text-input" type="number" value={target.positionX} onChange={(event) => onChange({ positionX: Number(event.target.value) })} /></label><label>Pozycja Y<input className="text-input" type="number" value={target.positionY} onChange={(event) => onChange({ positionY: Number(event.target.value) })} /></label><label>Szerokość<input className="text-input" type="number" min="1" value={target.width} onChange={(event) => onChange({ width: Number(event.target.value), fixedRefreshRateNumerator: 0, fixedRefreshRateDenominator: 0, refreshRateNumerator: 0, refreshRateDenominator: 0 })} /></label><label>Wysokość<input className="text-input" type="number" min="1" value={target.height} onChange={(event) => onChange({ height: Number(event.target.value), fixedRefreshRateNumerator: 0, fixedRefreshRateDenominator: 0, refreshRateNumerator: 0, refreshRateDenominator: 0 })} /></label></div></div>
    <div className="display-property-section"><span className="display-property-heading">Odświeżanie</span><div className="display-refresh-field"><div className="display-refresh-select"><select className="text-input" value={resolveRefreshSelectionKey(target, refreshOptions)} disabled={!isAutoHighest && refreshState.disabled} onChange={(event) => { if (event.target.value === 'auto') { onChange({ refreshPolicy: 'AutoHighest', fixedRefreshRateNumerator: 0, fixedRefreshRateDenominator: 0, refreshRateNumerator: 0, refreshRateDenominator: 0 }); return; } const mode = refreshOptions.find((candidate) => modeKey(candidate) === event.target.value); if (mode) onChange({ refreshPolicy: 'Fixed', fixedRefreshRateNumerator: mode.refreshRateNumerator, fixedRefreshRateDenominator: mode.refreshRateDenominator, refreshRateNumerator: 0, refreshRateDenominator: 0 }); }}><option value="auto">Najwyższe dostępne</option>{refreshOptions.map((mode) => <option key={modeKey(mode)} value={modeKey(mode)}>{formatRefresh(mode.refreshRateHz)}</option>)}{!isAutoHighest && refreshOptions.length === 0 && hasValidFixedRefresh(target) ? <option value={fixedModeKey(target)}>{formatRefresh((target.fixedRefreshRateNumerator ?? 0) / (target.fixedRefreshRateDenominator ?? 1))}</option> : null}{refreshOptions.length === 0 && !hasValidFixedRefresh(target) && !isAutoHighest ? <option value="" disabled>{refreshPlaceholder}</option> : null}</select><span className="display-refresh-chevron" aria-hidden="true" /></div></div>{observed && !observed.isEnabled && getRefreshPolicy(target) === 'Fixed' && hasValidFixedRefresh(target) ? <p className="hint-line">Pokazano zapisaną stałą częstotliwość profilu. Pełną listę trybów można zweryfikować po włączeniu ekranu.</p> : null}{!observed ? <p className="hint-line">Ekran nie jest obecnie wykryty. Zapisana konfiguracja pozostaje bez zmian.</p> : null}</div>
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

function resolveModeKey(target: ProfileDisplayTarget, modes: DisplayModeInfo[]) {
  const targetRefresh = target.refreshRateDenominator > 0 ? target.refreshRateNumerator / target.refreshRateDenominator : 0;
  const mode = modes.find((candidate) => Math.abs(candidate.refreshRateHz - targetRefresh) <= 0.25);
  return mode ? modeKey(mode) : '';
}

function formatRefresh(refreshRate: number) {
  return Number.isInteger(refreshRate) ? `${refreshRate} Hz` : `${refreshRate.toFixed(2)} Hz`;
}

function DisplayTopologyValidation({ errors, showSelectionError }: { errors: string[]; showSelectionError: boolean }) {
  if (!showSelectionError && errors.length === 0) return null;
  return <div className="display-topology-validation" role="alert">{(showSelectionError ? ['Dodaj co najmniej jeden ekran.', ...errors] : errors).filter((error, index, all) => all.indexOf(error) === index).map((error) => <span key={error}>{error}</span>)}</div>;
}

function identityKey(identity: ProfileDisplayTarget['identity'] | undefined): string {
  if (!identity) return '';
  return identity.monitorDevicePath || `${identity.adapterLuid}:${identity.targetId}:${identity.connectorInstance}`;
}

function getBounds(displays: ProfileDisplayTarget[]) {
  return computeTopologyBounds(displays.map(display => rectFromPosition(display.positionX, display.positionY, display.width, display.height)));
}
