// The panel's own vocabulary: what an asset, a work order and the service
// health are. Everything else (the HTTP client, the queries, the screens)
// depends on these types, never the other way around.

export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface Asset {
  id: number
  code: string
  name: string
  location: string | null
}

export type WorkOrderStatus = 'Open' | 'InProgress' | 'Completed' | 'Cancelled'

export interface WorkOrder {
  id: number
  assetId: number
  description: string
  status: WorkOrderStatus
  createdAt: string
  completedAt: string | null
  isSynced: boolean
}

export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy'

export interface HealthReport {
  status: HealthStatus
  totalDurationMs: number
  checks: { name: string; status: HealthStatus; description: string | null; durationMs: number }[]
}
