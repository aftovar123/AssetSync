import { screen, within } from '@testing-library/react'
import { compressor, health, openOrder, pump, queuedOrder, syncedOrder } from '../test-fixtures'
import { mockApi, paged, renderWithOperator } from '../test-utils'
import { OverviewPage } from './OverviewPage'

afterEach(() => vi.restoreAllMocks())

function serve(orders = [openOrder, queuedOrder, syncedOrder], totalCount = orders.length) {
  return mockApi({
    'GET /work-orders': { body: paged(orders, { pageSize: 100, totalCount }) },
    'GET /assets': { body: paged([pump, compressor], { pageSize: 100 }) },
    'GET /health': { body: health },
  })
}

describe('OverviewPage', () => {
  it('counts the orders at each station of the way to the ERP', async () => {
    serve()
    renderWithOperator(<OverviewPage />)

    const line = await screen.findByRole('region', { name: 'Camino al ERP' })
    expect(within(within(line).getByText('Abiertas').closest('li')!).getByText('1')).toBeInTheDocument()
    expect(within(within(line).getByText('En cola').closest('li')!).getByText('1')).toBeInTheDocument()
    expect(within(within(line).getByText('Sincronizadas').closest('li')!).getByText('1')).toBeInTheDocument()
  })

  it('lists recent orders with the asset code instead of its id', async () => {
    serve()
    renderWithOperator(<OverviewPage />)

    const recent = await screen.findByRole('region', { name: 'Órdenes recientes' })
    expect(await within(recent).findByText('COMP-004')).toBeInTheDocument()
    expect(within(recent).getAllByText('BOMBA-001')).toHaveLength(2)
    expect(within(recent).queryByText('2002')).not.toBeInTheDocument()
    expect(within(recent).getByText('Cambio de filtro')).toBeInTheDocument()
  })

  it('names each service in plain words and explains the one with warnings', async () => {
    serve()
    renderWithOperator(<OverviewPage />)

    const services = await screen.findByRole('region', { name: 'Servicios' })
    expect(await within(services).findByText('Mensajería (RabbitMQ)')).toBeInTheDocument()
    expect(within(services).getByText('Con avisos')).toBeInTheDocument()
    expect(within(services).getByText(/RabbitMQ no está configurado/)).toBeInTheDocument()
    expect(within(services).queryByText('Base de datos disponible.')).not.toBeInTheDocument()
  })

  it('says when the line only covers the latest orders', async () => {
    serve([openOrder], 250)
    renderWithOperator(<OverviewPage />)

    expect(await screen.findByText('Últimas 100 de 250 órdenes')).toBeInTheDocument()
  })

  it('offers no actions to a visitor who is not signed in', async () => {
    serve()
    renderWithOperator(<OverviewPage />)

    await screen.findByRole('region', { name: 'Órdenes recientes' })
    expect(screen.queryByRole('button', { name: /Completar/ })).not.toBeInTheDocument()
  })

  it('lets an operator complete only the orders still open', async () => {
    serve()
    renderWithOperator(<OverviewPage />, ['workorders.write'])

    expect(await screen.findByRole('button', { name: 'Completar la orden 3001' })).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /Completar la orden/ })).toHaveLength(1)
  })

  it('invites the first order when there are none', async () => {
    serve([])
    renderWithOperator(<OverviewPage />)

    expect(await screen.findByText('Todavía no hay órdenes de trabajo.')).toBeInTheDocument()
  })
})
