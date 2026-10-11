import type { Asset, HealthReport, WorkOrder } from './domain/models'

export const pump: Asset = { id: 2002, code: 'BOMBA-001', name: 'Bomba centrífuga principal', location: 'Planta Barranquilla' }
export const compressor: Asset = { id: 2003, code: 'COMP-004', name: 'Compresor de aire', location: null }

export function workOrder(overrides: Partial<WorkOrder> = {}): WorkOrder {
  return {
    id: 3001,
    assetId: pump.id,
    description: 'Cambio de rodamientos',
    status: 'Open',
    createdAt: '2026-10-05T20:27:42Z',
    completedAt: null,
    isSynced: false,
    ...overrides,
  }
}

export const openOrder = workOrder({ id: 3001 })
export const queuedOrder = workOrder({ id: 3002, description: 'Ajuste de correa', status: 'Completed', completedAt: '2026-10-05T20:31:24Z' })
export const syncedOrder = workOrder({
  id: 3003,
  assetId: compressor.id,
  description: 'Cambio de filtro',
  status: 'Completed',
  completedAt: '2026-10-05T20:10:00Z',
  isSynced: true,
})

export const health: HealthReport = {
  status: 'Degraded',
  totalDurationMs: 12,
  checks: [
    { name: 'database', status: 'Healthy', description: 'Base de datos disponible.', durationMs: 5 },
    { name: 'outbox', status: 'Healthy', description: 'Ningún aviso al ERP quedó fallido.', durationMs: 3 },
    { name: 'messaging', status: 'Degraded', description: 'RabbitMQ no está configurado (falta RabbitMq:ConnectionString).', durationMs: 0 },
  ],
}
