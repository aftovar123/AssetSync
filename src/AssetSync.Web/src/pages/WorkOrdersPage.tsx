import { useState } from 'react'
import { useAssetCodes, useWorkOrders } from '../api/queries'
import { LoadError } from '../components/LoadError'
import { Pager } from '../components/Pager'
import { OrderActions } from '../components/OrderActions'
import { SyncHistoryDialog } from '../components/SyncHistoryDialog'
import { NewWorkOrderForm } from '../components/NewWorkOrderForm'
import { WorkOrdersTable } from '../components/WorkOrdersTable'
import { useOperatorSession } from '../session/useOperatorSession'

const PAGE_SIZE = 20

export function WorkOrdersPage() {
  const [page, setPage] = useState(1)
  const orders = useWorkOrders(page, PAGE_SIZE)
  const assetCodes = useAssetCodes()
  const { can } = useOperatorSession()
  const canWrite = can('workorders.write')
  const hasActions = canWrite || can('integration.read')
  const [historyOf, setHistoryOf] = useState<number | null>(null)
  const [created, setCreated] = useState<number | null>(null)

  if (orders.isError) return <LoadError what="las órdenes de trabajo" onRetry={() => orders.refetch()} />

  const data = orders.data

  return (
    <section className="panel" aria-labelledby="orders-title">
      <div className="panel__header">
        <h1 id="orders-title">Órdenes de trabajo</h1>
        <p className="panel__hint">Se actualiza sola cada 5 segundos.</p>
      </div>
      {canWrite && (
        <NewWorkOrderForm
          onCreated={(id) => {
            setCreated(id)
            setPage(1)
          }}
        />
      )}
      {created !== null && (
        <p className="notice" role="status">
          Orden {created} creada.
        </p>
      )}
      {data && data.items.length === 0 ? (
        <p className="empty">Todavía no hay órdenes de trabajo.</p>
      ) : (
        <WorkOrdersTable
          orders={data?.items ?? []}
          assetCodes={assetCodes}
          showCreated
          actions={hasActions ? (order) => <OrderActions order={order} onShowHistory={setHistoryOf} /> : undefined}
        />
      )}
      <SyncHistoryDialog workOrderId={historyOf} onClose={() => setHistoryOf(null)} />
      {data && (
        <Pager page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onChange={setPage} />
      )}
    </section>
  )
}
