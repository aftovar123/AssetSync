import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { compressor, openOrder, pump, queuedOrder, syncedOrder, workOrder } from '../test-fixtures'
import { mockApi, paged, renderWithOperator } from '../test-utils'
import { WorkOrdersPage } from './WorkOrdersPage'

afterEach(() => vi.restoreAllMocks())

const assets = { body: paged([pump, compressor], { pageSize: 100 }) }

describe('WorkOrdersPage', () => {
  it('moves through the pages of orders', async () => {
    const fetchMock = mockApi({
      'GET /assets': assets,
      'GET /work-orders': (url) =>
        url.searchParams.get('page') === '2'
          ? { body: paged([workOrder({ id: 2999, description: 'Orden de la segunda página' })], { page: 2, totalCount: 21, totalPages: 2 }) }
          : { body: paged([openOrder], { totalCount: 21, totalPages: 2 }) },
    })
    renderWithOperator(<WorkOrdersPage />)

    expect(await screen.findByText('Página 1 de 2, 21 en total')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Siguiente' }))

    expect(await screen.findByText('Orden de la segunda página')).toBeInTheDocument()
    expect(screen.getByText('Página 2 de 2, 21 en total')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes('/work-orders?page=2&pageSize=20'))).toBe(true)
  })

  it('shows a visitor the list only', async () => {
    mockApi({ 'GET /assets': assets, 'GET /work-orders': { body: paged([openOrder, syncedOrder]) } })
    renderWithOperator(<WorkOrdersPage />)

    expect(await screen.findByText('Cambio de filtro')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Nueva orden de trabajo' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Completar|Ver envíos/ })).not.toBeInTheDocument()
  })

  it('lets an operator who can write create orders and complete open ones', async () => {
    mockApi({
      'GET /assets': assets,
      'GET /work-orders': { body: paged([openOrder, queuedOrder]) },
      'POST /work-orders': { status: 201, body: workOrder({ id: 3050, description: 'Revisión de sellos' }) },
    })
    renderWithOperator(<WorkOrdersPage />, ['workorders.write'])

    await userEvent.selectOptions(await screen.findByLabelText('Activo'), await screen.findByRole('option', { name: /BOMBA-001/ }))
    await userEvent.type(screen.getByLabelText('Trabajo a realizar'), 'Revisión de sellos')
    await userEvent.click(screen.getByRole('button', { name: 'Crear orden' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Orden 3050 creada.')
    expect(screen.getByRole('button', { name: 'Completar la orden 3001' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Completar la orden 3002' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ver envíos' })).not.toBeInTheDocument()
  })

  it('lets an operator who reads the integration see the attempts of completed orders', async () => {
    mockApi({ 'GET /assets': assets, 'GET /work-orders': { body: paged([openOrder, queuedOrder, syncedOrder]) } })
    renderWithOperator(<WorkOrdersPage />, ['integration.read'])

    await screen.findByText('Cambio de filtro')
    expect(screen.getAllByRole('button', { name: 'Ver envíos' })).toHaveLength(2)
    expect(screen.queryByRole('heading', { name: 'Nueva orden de trabajo' })).not.toBeInTheDocument()
  })

  it('explains a failed load and retries', async () => {
    let calls = 0
    mockApi({
      'GET /assets': assets,
      'GET /work-orders': () => (++calls === 1 ? { status: 503 } : { body: paged([openOrder]) }),
    })
    renderWithOperator(<WorkOrdersPage />)

    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudieron cargar las órdenes de trabajo.')
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }))

    expect(await screen.findByText('Cambio de rodamientos')).toBeInTheDocument()
  })
})
