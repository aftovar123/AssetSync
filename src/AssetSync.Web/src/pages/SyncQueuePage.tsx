import { useState } from 'react'
import { useOutbox } from '../api/queries'
import { LoadError } from '../components/LoadError'
import { Pager } from '../components/Pager'
import type { OutboxStatus } from '../domain/models'
import { formatDateTime } from '../lib/format'
import { useOperatorSession } from '../session/useOperatorSession'

const PAGE_SIZE = 20

const statusText: Record<OutboxStatus, string> = {
  Pending: 'Pendiente',
  Processing: 'Enviando',
  Processed: 'Enviado',
  Failed: 'Fallido',
}

/**
 * The outbox as an operator sees it: every notice to the ERP, whether it is
 * waiting, being sent, sent, or gave up after its retries.
 */
export function SyncQueuePage() {
  const { can } = useOperatorSession()
  const [page, setPage] = useState(1)
  const outbox = useOutbox(page, PAGE_SIZE)

  if (!can('integration.read')) {
    return (
      <section className="panel" aria-labelledby="queue-title">
        <h1 id="queue-title">Cola de sincronización</h1>
        <p className="empty">
          Para ver la cola, inicia sesión con un cliente que pueda ver la sincronización, como
          erp-integration.
        </p>
      </section>
    )
  }

  if (outbox.isError) return <LoadError what="la cola de sincronización" onRetry={() => outbox.refetch()} />

  const data = outbox.data

  return (
    <section className="panel" aria-labelledby="queue-title">
      <div className="panel__header">
        <h1 id="queue-title">Cola de sincronización</h1>
        <p className="panel__hint">Cada aviso al ERP, del más reciente al más antiguo. Se actualiza cada 5 segundos.</p>
      </div>
      {data && data.items.length === 0 ? (
        <p className="empty">La cola está vacía: todavía no se ha completado ninguna orden.</p>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Aviso</th>
              <th scope="col">Orden</th>
              <th scope="col">Estado</th>
              <th scope="col">Intentos fallidos</th>
              <th scope="col">Creado</th>
              <th scope="col">Enviado al ERP</th>
              <th scope="col">Último error</th>
            </tr>
          </thead>
          <tbody>
            {data?.items.map((message) => (
              <tr key={message.id}>
                <td className="num">{message.id}</td>
                <td className="num">{message.workOrderId}</td>
                <td>
                  <span className={`queue-status queue-status--${message.status.toLowerCase()}`}>
                    {statusText[message.status]}
                  </span>
                </td>
                <td className="num">{message.attempts}</td>
                <td className="when">{formatDateTime(message.createdAt)}</td>
                <td className="when">{formatDateTime(message.processedAt)}</td>
                <td>{message.lastError ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {data && (
        <Pager page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onChange={setPage} />
      )}
    </section>
  )
}
