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

/** What a token lets the operator do, one area each (OAuth2 scopes). */
export type Scope = 'assets.write' | 'workorders.write' | 'integration.read'

/**
 * An operator signed in with a client's credentials. Lives only in memory:
 * a reload signs the operator out, and nothing secret is ever stored.
 */
export interface OperatorSession {
  clientId: string
  scopes: Scope[]
  accessToken: string
  expiresAt: number
}

export type OutboxStatus = 'Pending' | 'Processing' | 'Processed' | 'Failed'

/** One pending notice to the ERP, written in the same transaction that completed the order. */
export interface OutboxMessage {
  id: number
  workOrderId: number
  createdAt: string
  claimedAt: string | null
  processedAt: string | null
  attempts: number
  lastError: string | null
  status: OutboxStatus
}

/** One attempt to send a work order to the ERP, with the idempotency code it carried. */
export interface IntegrationLog {
  id: number
  workOrderId: number
  submissionCode: string
  sent: boolean
  attemptedAt: string
  errorMessage: string | null
}
