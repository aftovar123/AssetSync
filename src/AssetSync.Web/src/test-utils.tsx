import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { MemoryRouter } from 'react-router-dom'
import type { Scope } from './domain/models'
import { OperatorSessionProvider } from './session/OperatorSessionProvider'

/** Renders with a fresh query cache and, optionally, a signed-in operator. */
export function renderWithOperator(ui: ReactElement, scopes: Scope[] | null = null) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const session = scopes && {
    clientId: 'erp-integration',
    scopes,
    accessToken: 'test-token',
    expiresAt: Date.now() + 3_600_000,
  }
  return render(
    <QueryClientProvider client={queryClient}>
      <OperatorSessionProvider initialSession={session}>
        <MemoryRouter>{ui}</MemoryRouter>
      </OperatorSessionProvider>
    </QueryClientProvider>,
  )
}

/** Answers every fetch call with the responses given, in order. */
export function mockFetch(...responses: { status: number; body?: unknown }[]) {
  const spy = vi.spyOn(globalThis, 'fetch')
  for (const { status, body } of responses) {
    spy.mockResolvedValueOnce(
      new Response(body === undefined ? null : JSON.stringify(body), {
        status,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
  }
  return spy
}

type Route = { status?: number; body?: unknown } | ((url: URL) => { status?: number; body?: unknown })

/**
 * A stand-in API for whole screens, which make several requests at once:
 * each "METHOD /path" answers from its route (query string ignored for the
 * match, but passed to function routes). Unknown routes answer 404.
 */
export function mockApi(routes: Record<string, Route>) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = new URL(String(input), 'http://localhost')
    const key = `${init?.method ?? 'GET'} ${url.pathname.replace(/^\/api/, '')}`
    const route = routes[key]
    const { status = 200, body } = route === undefined ? { status: 404 } : typeof route === 'function' ? route(url) : route
    return new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { 'Content-Type': 'application/json' },
    })
  })
}

/** A one-page result in the API's pagination envelope. */
export function paged<T>(items: T[], overrides: Partial<{ page: number; pageSize: number; totalCount: number; totalPages: number }> = {}) {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1, ...overrides }
}
