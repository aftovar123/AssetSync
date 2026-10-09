import type { WorkOrder } from '../domain/models'
import { useOperatorSession } from '../session/useOperatorSession'
import { CompleteOrderButton } from './CompleteOrderButton'

/** What the signed-in operator can do with one work order, by scope. */
export function OrderActions({ order, onShowHistory }: { order: WorkOrder; onShowHistory: (id: number) => void }) {
  const { can } = useOperatorSession()

  return (
    <span className="order-actions">
      {can('workorders.write') && <CompleteOrderButton order={order} />}
      {can('integration.read') && order.status === 'Completed' && (
        <button type="button" className="button--quiet" onClick={() => onShowHistory(order.id)}>
          Ver envíos
        </button>
      )}
    </span>
  )
}
