import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { compressor, pump } from '../test-fixtures'
import { mockApi, paged, renderWithOperator } from '../test-utils'
import { AssetsPage } from './AssetsPage'

afterEach(() => vi.restoreAllMocks())

describe('AssetsPage', () => {
  it('lists each asset with its code, name and location', async () => {
    mockApi({ 'GET /assets': { body: paged([pump, compressor]) } })
    renderWithOperator(<AssetsPage />)

    expect(await screen.findByText('BOMBA-001')).toBeInTheDocument()
    expect(screen.getByText('Planta Barranquilla')).toBeInTheDocument()
    expect(screen.getByText('Compresor de aire')).toBeInTheDocument()
  })

  it('shows the registration form only to an operator who can register assets', async () => {
    mockApi({ 'GET /assets': { body: paged([pump]) } })
    const { unmount } = renderWithOperator(<AssetsPage />, ['workorders.write', 'integration.read'])

    await screen.findByText('BOMBA-001')
    expect(screen.queryByRole('heading', { name: 'Nuevo activo' })).not.toBeInTheDocument()
    unmount()

    renderWithOperator(<AssetsPage />, ['assets.write'])
    expect(await screen.findByRole('heading', { name: 'Nuevo activo' })).toBeInTheDocument()
  })

  it('confirms a registered asset by its code', async () => {
    mockApi({
      'GET /assets': { body: paged([pump]) },
      'POST /assets': { status: 201, body: { id: 2010, code: 'BOMBA-002', name: 'Bomba de enfriamiento', location: null } },
    })
    renderWithOperator(<AssetsPage />, ['assets.write'])

    await userEvent.type(await screen.findByLabelText('Código'), 'bomba-002')
    await userEvent.type(screen.getByLabelText('Nombre'), 'Bomba de enfriamiento')
    await userEvent.click(screen.getByRole('button', { name: 'Registrar activo' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Activo BOMBA-002 registrado.')
  })

  it('invites the first asset when there are none', async () => {
    mockApi({ 'GET /assets': { body: paged([]) } })
    renderWithOperator(<AssetsPage />)

    expect(await screen.findByText('Todavía no hay activos registrados.')).toBeInTheDocument()
  })
})
