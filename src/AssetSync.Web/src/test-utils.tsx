import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
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
      <OperatorSessionProvider initialSession={session}>{ui}</OperatorSessionProvider>
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
