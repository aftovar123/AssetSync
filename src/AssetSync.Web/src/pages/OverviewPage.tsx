import { useState } from 'react'
import { Link } from 'react-router-dom'
import type { HealthStatus } from '../domain/models'
import { useAssetCodes, useHealth, useWorkOrders } from '../api/queries'
import { EmptyState } from '../components/EmptyState'
import { LoadError } from '../components/LoadError'
import { Loading } from '../components/Loading'
import { SyncLine } from '../components/SyncLine'
import { OrderActions } from '../components/OrderActions'
import { SyncHistoryDialog } from '../components/SyncHistoryDialog'
import { WorkOrdersTable } from '../components/WorkOrdersTable'
import { useOperatorSession } from '../session/useOperatorSession'
import { countByStage } from '../domain/sync'

const RECENT_WINDOW = 100

const checkNames: Record<string, string> = {
  database: 'Base de datos',
  outbox: 'Cola de sincronización',
  messaging: 'Mensajería (RabbitMQ)',
}

const checkStatus: Record<HealthStatus, string> = {
  Healthy: 'Funcionando',
  Degraded: 'Con avisos',
  Unhealthy: 'Con fallas',
}

export function OverviewPage() {
  const orders = useWorkOrders(1, RECENT_WINDOW)
  const health = useHealth()
  const assetCodes = useAssetCodes()
  const { can } = useOperatorSession()
  const hasActions = can('workorders.write') || can('integration.read')
  const [historyOf, setHistoryOf] = useState<number | null>(null)

  if (orders.isError) return <LoadError what="las órdenes de trabajo" onRetry={() => orders.refetch()} />
  if (!orders.data) return <Loading what="el resumen" />

  const items = orders.data.items
  const total = orders.data.totalCount
  const scopeNote = total > RECENT_WINDOW ? `Últimas ${RECENT_WINDOW} de ${total} órdenes` : undefined

  return (
    <div className="overview">
      {/* Drawn only once data is in (see the early return above), so the
          first load does not look like a sync. */}
      <SyncLine counts={countByStage(items)} scopeNote={scopeNote} />

      <div className="overview__grid">
        <section className="panel" aria-labelledby="recent-title">
          <div className="panel__header">
            <h2 id="recent-title">Órdenes recientes</h2>
            <Link to="/ordenes">Ver todas</Link>
          </div>
          {items.length === 0 ? (
            <EmptyState
              message="Todavía no hay órdenes de trabajo."
              firstItem="la primera orden"
              createScope="workorders.write"
              createHint="Créala en Órdenes de trabajo."
              createdBy="erp-integration"
            />
          ) : (
            <WorkOrdersTable
              orders={items.slice(0, 8)}
              assetCodes={assetCodes}
              actions={hasActions ? (order) => <OrderActions order={order} onShowHistory={setHistoryOf} /> : undefined}
            />
          )}
          <SyncHistoryDialog workOrderId={historyOf} onClose={() => setHistoryOf(null)} />
        </section>

        <section className="panel" aria-labelledby="services-title">
          <div className="panel__header">
            <h2 id="services-title">Servicios</h2>
          </div>
          {health.isError ? (
            <p className="empty">No se pudo consultar el estado de la API.</p>
          ) : !health.data ? (
            <Loading what="el estado de los servicios" />
          ) : (
            <ul className="checks">
              {health.data.checks.map((check) => (
                <li key={check.name} className={`check check--${check.status.toLowerCase()}`}>
                  <span className="check__name">{checkNames[check.name] ?? check.name}</span>
                  <span className="check__status">{checkStatus[check.status]}</span>
                  {check.status !== 'Healthy' && check.description && (
                    <span className="check__detail">{check.description}</span>
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  )
}
