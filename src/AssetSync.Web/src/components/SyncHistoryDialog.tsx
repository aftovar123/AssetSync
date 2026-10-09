import { useEffect, useRef } from 'react'
import { useIntegrationLogs } from '../api/queries'
import { formatDateTime } from '../lib/format'

/**
 * Every attempt to send one work order to the ERP. All attempts for the same
 * order share one submission code, which is how the ERP recognises a retry
 * and never records the order twice.
 */
export function SyncHistoryDialog({ workOrderId, onClose }: { workOrderId: number | null; onClose: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null)
  const logs = useIntegrationLogs(workOrderId)

  useEffect(() => {
    const element = dialog.current
    if (!element) return
    if (workOrderId !== null && !element.open) element.showModal()
    if (workOrderId === null && element.open) element.close()
  }, [workOrderId])

  return (
    <dialog ref={dialog} className="dialog dialog--wide" onClose={onClose} aria-labelledby="history-title">
      <h2 id="history-title">Envíos al ERP de la orden {workOrderId}</h2>
      {logs.isPending && <p className="empty">Cargando…</p>}
      {logs.isError && <p className="form-error">{logs.error.message}</p>}
      {logs.data && logs.data.items.length === 0 && (
        <p className="empty">Todavía no se ha intentado enviar esta orden. Se envía después de completarla.</p>
      )}
      {logs.data && logs.data.items.length > 0 && (
        <ol className="attempts">
          {logs.data.items.map((log) => (
            <li key={log.id} className={log.sent ? 'attempt attempt--sent' : 'attempt attempt--failed'}>
              <span className="attempt__result">{log.sent ? 'Recibido por el ERP' : 'Falló'}</span>
              <span className="when">{formatDateTime(log.attemptedAt)}</span>
              <span className="attempt__code">
                Código de envío <code>{log.submissionCode}</code>
              </span>
              {log.errorMessage && <span className="attempt__error">{log.errorMessage}</span>}
            </li>
          ))}
        </ol>
      )}
      <div className="dialog__actions">
        <button type="button" onClick={onClose}>
          Cerrar
        </button>
      </div>
    </dialog>
  )
}
