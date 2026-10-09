import { useCompleteWorkOrder } from '../api/mutations'
import type { WorkOrder } from '../domain/models'

/**
 * Marks the work done. The API answers 202 right away and the outbox takes
 * the order to the ERP in the background, so the row moves to "En cola" and,
 * a few seconds later, to "Sincronizada" on its own.
 */
export function CompleteOrderButton({ order }: { order: WorkOrder }) {
  const complete = useCompleteWorkOrder()

  if (order.status === 'Completed' || order.status === 'Cancelled') return null

  return (
    <span className="row-action">
      <button
        type="button"
        onClick={() => complete.mutate(order.id)}
        disabled={complete.isPending}
        aria-label={`Completar la orden ${order.id}`}
      >
        {complete.isPending ? 'Completando…' : 'Completar'}
      </button>
      {complete.error && (
        <small className="field__error" role="alert">
          {complete.error.message}
        </small>
      )}
    </span>
  )
}
