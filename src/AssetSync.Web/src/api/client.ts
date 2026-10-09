// Typed access to the AssetSync API: the only place that knows URLs and
// fetch. Only the public, read-only endpoints live here for now; the
// operator's actions (which need a token) come later.
import type { Asset, HealthReport, Paged, WorkOrder } from '../domain/models'

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '/api'

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

async function get<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, { signal, headers: { Accept: 'application/json' } })
  // /health answers 503 with a full report when something is down; that is
  // still a report worth showing, not a failed request.
  if (!response.ok && !(path === '/health' && response.status === 503)) {
    throw new ApiError(response.status, `La API respondió ${response.status} en ${path}.`)
  }
  return (await response.json()) as T
}

export const api = {
  health: (signal?: AbortSignal) => get<HealthReport>('/health', signal),
  assets: (page: number, pageSize: number, signal?: AbortSignal) =>
    get<Paged<Asset>>(`/assets?page=${page}&pageSize=${pageSize}`, signal),
  workOrders: (page: number, pageSize: number, signal?: AbortSignal) =>
    get<Paged<WorkOrder>>(`/work-orders?page=${page}&pageSize=${pageSize}`, signal),
}
