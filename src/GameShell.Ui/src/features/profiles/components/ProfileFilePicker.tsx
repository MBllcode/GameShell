import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sendBridgeCommand } from '../../../bridge/webViewBridge';

type SearchResult = { paths: string[]; scanning: boolean; limited: boolean; indexedCount: number };
export function ProfileFilePicker({ onPick, onClose }: { onPick: (path: string) => void; onClose: () => void }) {
  const { i18n } = useTranslation();
  const pl = i18n.language.startsWith('pl');
  const [query, setQuery] = useState('');
  const [selected, setSelected] = useState('');
  const searchInput = useRef<HTMLInputElement>(null);
  const [result, setResult] = useState<SearchResult | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => { dialog.current?.showModal(); searchInput.current?.focus(); }, []);
  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout>;
    setResult(null);
    const search = async () => {
      try {
        const next = await sendBridgeCommand<SearchResult>('searchProfileFiles', { query });
        if (!cancelled) {
          setResult(next);
          if (next.scanning) timer = setTimeout(() => void search(), 1200);
        }
      } catch { if (!cancelled) setError(pl ? 'Wyszukiwanie jest niedostępne.' : 'Search is unavailable.'); }
    };
    timer = setTimeout(() => void search(), 300);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [query, pl]);
  const choose = async (path: string) => {
    setBusy(true); setError('');
    try { onPick(await sendBridgeCommand<string>('validateProfileFile', { path })); }
    catch { setError(pl ? 'Plik nie istnieje lub jest niedostępny. Sprawdź pełną ścieżkę.' : 'File not found. Check its full path.'); }
    finally { setBusy(false); }
  };
  const browse = async () => {
    try { const path = await sendBridgeCommand<string>('selectProfileExecutable'); if (path) await choose(path); }
    catch { setError(pl ? 'Nie udało się otworzyć wyboru pliku.' : 'Could not open the file picker.'); }
  };
  const typedPath = /^(?:[a-z]:[\\/]|\\\\)/i.test(query.trim().replace(/^"|"$/g, ''));
  const candidate = selected || (typedPath ? query.trim() : '');
  const searching = query.trim().length >= 2 && !typedPath;
  return <dialog ref={dialog} className="profile-file-picker" onCancel={event => { event.preventDefault(); onClose(); }} aria-labelledby="file-picker-title">
    <form onSubmit={event => { event.preventDefault(); if (candidate && !busy) void choose(candidate); }}>
      <header><h2 id="file-picker-title">{pl ? 'Dodaj aplikację lub plik' : 'Add an application or file'}</h2><button type="button" className="file-picker-close" aria-label={pl ? 'Zamknij' : 'Close'} onClick={onClose}>×</button></header>
      <label className="file-picker-search"><span>{pl ? 'Wyszukaj po nazwie lub wklej ścieżkę' : 'Search by name or paste a path'}</span><input ref={searchInput} value={query} onChange={event => { setQuery(event.target.value); setSelected(''); setError(''); }} placeholder={pl ? 'np. Chrome, Steam...' : 'e.g. Chrome, Steam...'} autoComplete="off" /></label>
      <div className="file-picker-results">
        {searching ? result?.paths.map(path => <button className="file-picker-result" type="button" key={path} disabled={busy} aria-pressed={selected === path} onClick={() => setSelected(path)} onDoubleClick={() => { if (!busy) void choose(path); }}><span className="file-picker-check" aria-hidden="true">{selected === path ? '✓' : '+'}</span><span><strong>{path.split(/[\\/]/).pop()}</strong><small>{path}</small></span></button>) : null}
        {!searching && !typedPath ? <div className="file-picker-empty">{pl ? 'Znajdź plik, który ma otwierać ten profil.' : 'Find a file for this profile to open.'}</div> : null}
        {typedPath ? <div className="file-picker-empty">{pl ? 'Zatwierdź ścieżkę przyciskiem Dodaj.' : 'Confirm this path with Add.'}</div> : null}
        {searching && (!result || result.scanning) ? <p className="file-picker-status" role="status">{pl ? 'Wyszukiwanie na dyskach…' : 'Searching drives…'}</p> : null}
        {searching && result && !result.scanning && !result.paths.length ? <div className="file-picker-empty"><strong>{pl ? 'Nie znaleziono pliku' : 'No files found'}</strong><span>{pl ? 'Spróbuj innej nazwy lub wybierz plik ręcznie.' : 'Try another name or choose the file manually.'}</span></div> : null}
      </div>
      {searching && result?.limited && !result.scanning ? <p className="file-picker-status">{pl ? 'Część plików może być niedostępna. Możesz wskazać plik ręcznie.' : 'Some files may be unavailable. You can choose a file manually.'}</p> : null}
      {error ? <p role="alert" className="error-line">{error}</p> : null}
      <footer className="file-picker-footer"><button type="button" className="secondary-button" disabled={busy} onClick={() => void browse()}>{pl ? 'Wybierz plik…' : 'Choose file…'}</button><div><button type="button" className="secondary-button" onClick={onClose}>{pl ? 'Anuluj' : 'Cancel'}</button><button type="submit" className="primary-button" disabled={busy || !candidate}>{busy ? (pl ? 'Dodawanie…' : 'Adding…') : (pl ? 'Dodaj' : 'Add')}</button></div></footer>
    </form>
  </dialog>;
}
