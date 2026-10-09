import type { WorkOrder } from './models'

// Where a work order is on its way to the ERP. Derived from the public work
// order data alone: an order is queued from the moment it is completed until
// the outbox processor confirms the ERP received it.
export type SyncStage = 'open' | 'queued' | 'synced' | 'cancelled'

export function syncStage(order: WorkOrder): SyncStage {
  if (order.status === 'Cancelled') return 'cancelled'
  if (order.isSynced) return 'synced'
  if (order.status === 'Completed') return 'queued'
  return 'open'
}

export interface SyncLineCounts {
  open: number
  queued: number
  synced: number
}

export function countByStage(orders: WorkOrder[]): SyncLineCounts {
  const counts: SyncLineCounts = { open: 0, queued: 0, synced: 0 }
  for (const order of orders) {
    const stage = syncStage(order)
    if (stage !== 'cancelled') counts[stage] += 1
  }
  return counts
}
