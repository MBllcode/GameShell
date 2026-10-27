import { useRef } from 'react';
import { useTranslation } from 'react-i18next';

export function LicenseNotices() {
  const { t } = useTranslation();
  const dialogRef = useRef<HTMLDialogElement>(null);
  return (
    <div className="license-notices-link">
      <button type="button" className="secondary-button compact" onClick={() => dialogRef.current?.showModal()}>
        {t('licenses.title')}
      </button>
      <dialog ref={dialogRef} className="license-dialog" aria-labelledby="license-dialog-title">
        <header>
          <h2 id="license-dialog-title">{t('licenses.title')}</h2>
          <form method="dialog"><button className="secondary-button" autoFocus>{t('licenses.close')}</button></form>
        </header>
        <iframe title={t('licenses.title')} src="./licenses.html" />
      </dialog>
    </div>
  );
}
