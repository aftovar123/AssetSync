import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { OperatorSessionProvider } from '../session/OperatorSessionProvider'
import { OperatorStatus } from './OperatorStatus'

function renderStatus() {
  return render(
    <OperatorSessionProvider>
      <OperatorStatus />
    </OperatorSessionProvider>,
  )
}

function respondWith(status: number, body: unknown) {
  return vi.spyOn(globalThis, 'fetch').mockResolvedValue(
    new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }),
  )
}

afterEach(() => vi.restoreAllMocks())

describe('OperatorStatus', () => {
  it('signs the operator in and shows what the token allows', async () => {
    const fetchMock = respondWith(200, {
      access_token: 'token-123',
      token_type: 'Bearer',
      expires_in: 3600,
      scope: 'workorders.write integration.read',
    })
    renderStatus()

    await userEvent.click(screen.getByRole('button', { name: 'Iniciar sesión como operador' }))
    await userEvent.type(screen.getByLabelText('Secreto del cliente'), 'the-secret')
    await userEvent.click(screen.getByRole('button', { name: 'Iniciar sesión' }))

    expect(await screen.findByText('erp-integration')).toBeInTheDocument()
    expect(screen.getByText('Puede crear y completar órdenes y ver la sincronización')).toBeInTheDocument()

    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/auth/token')
    const form = new URLSearchParams(String(init?.body))
    expect(form.get('grant_type')).toBe('client_credentials')
    expect(form.get('client_id')).toBe('erp-integration')
    expect(form.get('client_secret')).toBe('the-secret')
  })

  it('explains a wrong secret instead of signing in', async () => {
    respondWith(401, { error: 'invalid_client' })
    renderStatus()

    await userEvent.click(screen.getByRole('button', { name: 'Iniciar sesión como operador' }))
    await userEvent.type(screen.getByLabelText('Secreto del cliente'), 'wrong')
    await userEvent.click(screen.getByRole('button', { name: 'Iniciar sesión' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('El identificador o el secreto del cliente no son correctos.')
    expect(screen.queryByRole('button', { name: 'Cerrar sesión' })).not.toBeInTheDocument()
  })

  it('signs out and forgets the token', async () => {
    respondWith(200, { access_token: 't', token_type: 'Bearer', expires_in: 3600, scope: 'assets.write' })
    renderStatus()

    await userEvent.click(screen.getByRole('button', { name: 'Iniciar sesión como operador' }))
    await userEvent.type(screen.getByLabelText('Secreto del cliente'), 'the-secret')
    await userEvent.click(screen.getByRole('button', { name: 'Iniciar sesión' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Cerrar sesión' }))

    expect(screen.getByRole('button', { name: 'Iniciar sesión como operador' })).toBeInTheDocument()
  })
})
