import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { WorkOrder } from '../domain/models'
import { mockFetch, renderWithOperator } from '../test-utils'
import { CompleteOrderButton } from './CompleteOrderButton'
import { NewWorkOrderForm } from './NewWorkOrderForm'

afterEach(() => vi.restoreAllMocks())

const openOrder: WorkOrder = {
  id: 3002,
  assetId: 2002,
  description: 'Cambio de rodamientos',
  status: 'Open',
  createdAt: '2026-10-05T20:27:42Z',
  completedAt: null,
  isSynced: false,
}

const assetsPage = {
  items: [{ id: 2002, code: 'BOMBA-001', name: 'Bomba centrífuga principal', location: 'Planta Barranquilla' }],
  page: 1,
  pageSize: 100,
  totalCount: 1,
  totalPages: 1,
}

describe('CompleteOrderButton', () => {
  it('completes the order with the operator token', async () => {
    const fetchMock = mockFetch({ status: 202 })
    renderWithOperator(<CompleteOrderButton order={openOrder} />, ['workorders.write'])

    await userEvent.click(screen.getByRole('button', { name: 'Completar la orden 3002' }))

    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/work-orders/3002/complete')
    expect(init?.method).toBe('POST')
    expect(new Headers(init?.headers).get('Authorization')).toBe('Bearer test-token')
  })

  it('is not offered for an order that is already completed', () => {
    renderWithOperator(<CompleteOrderButton order={{ ...openOrder, status: 'Completed' }} />, ['workorders.write'])

    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('explains a missing permission', async () => {
    mockFetch({ status: 403 })
    renderWithOperator(<CompleteOrderButton order={openOrder} />, ['workorders.write'])

    await userEvent.click(screen.getByRole('button', { name: 'Completar la orden 3002' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Tu cliente no tiene permiso para esta acción.')
  })
})

describe('NewWorkOrderForm', () => {
  it('creates an order on the asset picked by its code', async () => {
    const fetchMock = mockFetch({ status: 200, body: assetsPage }, { status: 201, body: { ...openOrder, id: 3010 } })
    const onCreated = vi.fn()
    renderWithOperator(<NewWorkOrderForm onCreated={onCreated} />, ['workorders.write'])

    await userEvent.selectOptions(await screen.findByLabelText('Activo'), await screen.findByRole('option', { name: /BOMBA-001/ }))
    await userEvent.type(screen.getByLabelText('Trabajo a realizar'), 'Cambio de sellos')
    await userEvent.click(screen.getByRole('button', { name: 'Crear orden' }))

    await vi.waitFor(() => expect(onCreated).toHaveBeenCalledWith(3010))
    const [, init] = fetchMock.mock.calls[1]
    expect(JSON.parse(String(init?.body))).toEqual({ assetId: 2002, description: 'Cambio de sellos' })
  })

  it('shows the API validation message next to its field', async () => {
    mockFetch(
      { status: 200, body: assetsPage },
      { status: 400, body: { errors: { Description: ["'Descripción' no debería estar vacío."] } } },
    )
    renderWithOperator(<NewWorkOrderForm onCreated={vi.fn()} />, ['workorders.write'])

    await userEvent.selectOptions(await screen.findByLabelText('Activo'), await screen.findByRole('option', { name: /BOMBA-001/ }))
    await userEvent.type(screen.getByLabelText('Trabajo a realizar'), ' x ')
    await userEvent.click(screen.getByRole('button', { name: 'Crear orden' }))

    expect(await screen.findByText("'Descripción' no debería estar vacío.")).toBeInTheDocument()
  })
})
