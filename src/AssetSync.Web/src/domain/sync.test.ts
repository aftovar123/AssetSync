import type { WorkOrder } from './models'
import { countByStage, syncStage } from './sync'

function order(overrides: Partial<WorkOrder>): WorkOrder {
  return {
    id: 1,
    assetId: 1,
    description: 'Cambio de rodamientos',
    status: 'Open',
    createdAt: '2026-10-05T20:27:42Z',
    completedAt: null,
    isSynced: false,
    ...overrides,
  }
}

describe('syncStage', () => {
  it('treats an order still being worked on as open', () => {
    expect(syncStage(order({ status: 'Open' }))).toBe('open')
    expect(syncStage(order({ status: 'InProgress' }))).toBe('open')
  })

  it('queues a completed order until the ERP confirms it', () => {
    expect(syncStage(order({ status: 'Completed', isSynced: false }))).toBe('queued')
  })

  it('marks an order synced once the ERP confirmed it', () => {
    expect(syncStage(order({ status: 'Completed', isSynced: true }))).toBe('synced')
  })

  it('keeps cancelled orders out of the line', () => {
    expect(syncStage(order({ status: 'Cancelled' }))).toBe('cancelled')
  })
})

describe('countByStage', () => {
  it('counts each order in the station it is at, ignoring cancelled ones', () => {
    const counts = countByStage([
      order({ status: 'Open' }),
      order({ status: 'Completed' }),
      order({ status: 'Completed' }),
      order({ status: 'Completed', isSynced: true }),
      order({ status: 'Cancelled' }),
    ])

    expect(counts).toEqual({ open: 1, queued: 2, synced: 1 })
  })
})
