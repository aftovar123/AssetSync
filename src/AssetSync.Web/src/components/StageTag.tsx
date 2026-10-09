import type { WorkOrder } from '../domain/models'
import { syncStage, type SyncStage } from '../domain/sync'

const labels: Record<SyncStage, string> = {
  open: 'Abierta',
  queued: 'En cola',
  synced: 'Sincronizada',
  cancelled: 'Cancelada',
}

/** One word for where the order stands, coloured only when it needs attention. */
export function StageTag({ order }: { order: WorkOrder }) {
  const stage = syncStage(order)
  return <span className={`stage-tag stage-tag--${stage}`}>{labels[stage]}</span>
}
