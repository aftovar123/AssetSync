// Typed access to the AssetSync API: the only place that knows URLs and
// fetch. Public reads need nothing; the operator's actions send the token
// from the in-memory session as a Bearer header.
import type { Asset, HealthReport, Paged, Scope, WorkOrder } from '../domain/models'

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

/** A 400 with per-field messages, keyed by camelCase field name. */
export class ValidationError extends ApiError {
  readonly fields: Record<string, string[]>

  constructor(fields: Record<string, string[]>) {
    super(400, 'Revisa los campos marcados.')
    this.fields = fields
  }
}

const statusMessages: Record<number, string> = {
  401: 'Tu sesión de operador expiró. Inicia sesión de nuevo.',
  403: 'Tu cliente no tiene permiso para esta acción.',
  404: 'El registro ya no existe.',
}

/** An operator action: sends the session token and explains failures. */
async function send<T>(path: string, accessToken: string, body?: unknown): Promise<T | undefined> {
  const response = await fetch(`${baseUrl}${path}`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${accessToken}`,
      Accept: 'application/json',
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  if (response.status === 400) {
    const problem = (await response.json().catch(() => ({}))) as { errors?: Record<string, string[]> }
    throw new ValidationError(camelCaseKeys(problem.errors ?? {}))
  }
  if (!response.ok) {
    throw new ApiError(response.status, statusMessages[response.status] ?? `La API respondió ${response.status}.`)
  }
  // 202 Accepted and other empty answers have no body to read.
  const text = await response.text()
  return text ? (JSON.parse(text) as T) : undefined
}

function camelCaseKeys(errors: Record<string, string[]>): Record<string, string[]> {
  return Object.fromEntries(Object.entries(errors).map(([key, value]) => [key.charAt(0).toLowerCase() + key.slice(1), value]))
}

export interface IssuedToken {
  accessToken: string
  scopes: Scope[]
  expiresInSeconds: number
}

/**
 * Exchanges a client's credentials for a token (OAuth2 client credentials).
 * The secret goes straight to the API and is not kept anywhere afterwards.
 */
async function requestToken(clientId: string, clientSecret: string): Promise<IssuedToken> {
  const response = await fetch(`${baseUrl}/auth/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded', Accept: 'application/json' },
    body: new URLSearchParams({ grant_type: 'client_credentials', client_id: clientId, client_secret: clientSecret }),
  })
  if (response.status === 401) {
    throw new ApiError(401, 'El identificador o el secreto del cliente no son correctos.')
  }
  if (!response.ok) {
    throw new ApiError(response.status, `No se pudo iniciar sesión (la API respondió ${response.status}).`)
  }
  const body = (await response.json()) as { access_token: string; expires_in: number; scope: string }
  return {
    accessToken: body.access_token,
    scopes: body.scope.split(' ').filter(Boolean) as Scope[],
    expiresInSeconds: body.expires_in,
  }
}

export const api = {
  requestToken,
  health: (signal?: AbortSignal) => get<HealthReport>('/health', signal),
  assets: (page: number, pageSize: number, signal?: AbortSignal) =>
    get<Paged<Asset>>(`/assets?page=${page}&pageSize=${pageSize}`, signal),
  workOrders: (page: number, pageSize: number, signal?: AbortSignal) =>
    get<Paged<WorkOrder>>(`/work-orders?page=${page}&pageSize=${pageSize}`, signal),
  createWorkOrder: async (accessToken: string, order: { assetId: number; description: string }) =>
    (await send<WorkOrder>('/work-orders', accessToken, order))!,
  completeWorkOrder: async (accessToken: string, id: number) => {
    await send(`/work-orders/${id}/complete`, accessToken)
  },
}
