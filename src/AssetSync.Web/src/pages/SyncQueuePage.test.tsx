import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { OrderActions } from '../components/OrderActions'
import { SyncHistoryDialog } from '../components/SyncHistoryDialog'
import type { WorkOrder } from '../domain/models'
import { mockFetch, renderWithOperator } from '../test-utils'
import { SyncQueuePage } from './SyncQueuePage'

afterEach(() => vi.restoreAllMocks())

const page = <T,>(items: T[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 })

describe('SyncQueuePage', () => {
  it('asks for an operator who can read the integration state', () => {
    renderWithOperator(<SyncQueuePage />, ['assets.write'])

    expect(screen.getByText(/inicia sesión con un cliente que pueda ver la sincronización/)).toBeInTheDocument()
  })

  it('lists each notice to the ERP with its state in plain words', async () => {
    const fetchMock = mockFetch({
      status: 200,
      body: page([
        { id: 2, workOrderId: 3002, createdAt: '2026-10-05T20:31:24Z', claimedAt: '2026-10-05T20:31:31Z', processedAt: '2026-10-05T20:31:31Z', attempts: 0, lastError: null, status: 'Processed' },
        { id: 3, workOrderId: 3003, createdAt: '2026-10-05T20:40:00Z', claimedAt: null, processedAt: null, attempts: 5, lastError: 'ERP timeout', status: 'Failed' },
      ]),
    })
    renderWithOperator(<SyncQueuePage />, ['integration.read'])

    expect(await screen.findByText('ERP timeout')).toBeInTheDocument()
    expect(screen.getByText('Enviado')).toBeInTheDocument()
    expect(screen.getByText('Fallido')).toBeInTheDocument()
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get('Authorization')).toBe('Bearer test-token')
  })
})

describe('Sync history', () => {
  const completed: WorkOrder = {
    id: 3002,
    assetId: 2002,
    description: 'Cambio de rodamientos',
    status: 'Completed',
    createdAt: '2026-10-05T20:27:42Z',
    completedAt: '2026-10-05T20:31:24Z',
    isSynced: true,
  }

  it('is offered only for completed orders', () => {
    renderWithOperator(<OrderActions order={{ ...completed, status: 'Open' }} onShowHistory={vi.fn()} />, ['integration.read'])

    expect(screen.queryByRole('button', { name: 'Ver envíos' })).not.toBeInTheDocument()
  })

  it('opens the history of the chosen order', async () => {
    const onShowHistory = vi.fn()
    renderWithOperator(<OrderActions order={completed} onShowHistory={onShowHistory} />, ['integration.read'])

    await userEvent.click(screen.getByRole('button', { name: 'Ver envíos' }))

    expect(onShowHistory).toHaveBeenCalledWith(3002)
  })

  it('shows every attempt with the submission code they share', async () => {
    mockFetch({
      status: 200,
      body: page([
        { id: 11, workOrderId: 3002, submissionCode: '7a68f188d28f', sent: true, attemptedAt: '2026-10-05T20:31:40Z', errorMessage: null },
        { id: 10, workOrderId: 3002, submissionCode: '7a68f188d28f', sent: false, attemptedAt: '2026-10-05T20:31:31Z', errorMessage: 'Connection reset' },
      ]),
    })
    renderWithOperator(<SyncHistoryDialog workOrderId={3002} onClose={vi.fn()} />, ['integration.read'])

    expect(await screen.findByText('Recibido por el ERP')).toBeInTheDocument()
    expect(screen.getByText('Falló')).toBeInTheDocument()
    expect(screen.getAllByText('7a68f188d28f')).toHaveLength(2)
    expect(screen.getByText('Connection reset')).toBeInTheDocument()
  })
})
