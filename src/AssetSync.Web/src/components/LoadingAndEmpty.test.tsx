import { act, screen } from '@testing-library/react'
import { renderWithOperator } from '../test-utils'
import { EmptyState } from './EmptyState'
import { Loading } from './Loading'

describe('Loading', () => {
  afterEach(() => vi.useRealTimers())

  it('explains a slow first answer after a few seconds', () => {
    vi.useFakeTimers()
    renderWithOperator(<Loading what="las órdenes de trabajo" />)

    expect(screen.getByRole('status')).toHaveTextContent('Cargando las órdenes de trabajo…')
    expect(screen.queryByText(/se pausa cuando no se usa/)).not.toBeInTheDocument()

    act(() => vi.advanceTimersByTime(4_000))

    expect(screen.getByText(/se pausa cuando no se usa/)).toBeInTheDocument()
  })
})

describe('EmptyState', () => {
  const props = {
    message: 'Todavía no hay activos registrados.',
    firstItem: 'el primer activo',
    createScope: 'assets.write' as const,
    createHint: 'Registra el primero con el formulario de arriba.',
    createdBy: 'asset-admin',
  }

  it('invites a visitor to sign in', () => {
    renderWithOperator(<EmptyState {...props} />)

    expect(screen.getByText('Inicia sesión como operador para crear el primer activo.')).toBeInTheDocument()
  })

  it('points an operator without the permission to the right client', () => {
    renderWithOperator(<EmptyState {...props} />, ['workorders.write'])

    expect(screen.getByText('Para crear el primer activo, inicia sesión como asset-admin.')).toBeInTheDocument()
  })

  it('tells an operator with the permission where to create it', () => {
    renderWithOperator(<EmptyState {...props} />, ['assets.write'])

    expect(screen.getByText('Registra el primero con el formulario de arriba.')).toBeInTheDocument()
  })
})
