import { useState } from 'react'
import { useAssetCodes, useWorkOrders } from '../api/queries'
import { LoadError } from '../components/LoadError'
import { Pager } from '../components/Pager'
import { WorkOrdersTable } from '../components/WorkOrdersTable'

const PAGE_SIZE = 20

export function WorkOrdersPage() {
  const [page, setPage] = useState(1)
  const orders = useWorkOrders(page, PAGE_SIZE)
  const assetCodes = useAssetCodes()

  if (orders.isError) return <LoadError what="las órdenes de trabajo" onRetry={() => orders.refetch()} />

  const data = orders.data

  return (
    <section className="panel" aria-labelledby="orders-title">
      <div className="panel__header">
        <h1 id="orders-title">Órdenes de trabajo</h1>
        <p className="panel__hint">Se actualiza sola cada 5 segundos.</p>
      </div>
      {data && data.items.length === 0 ? (
        <p className="empty">Todavía no hay órdenes de trabajo.</p>
      ) : (
        <WorkOrdersTable orders={data?.items ?? []} assetCodes={assetCodes} showCreated />
      )}
      {data && (
        <Pager page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onChange={setPage} />
      )}
    </section>
  )
}
