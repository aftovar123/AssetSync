import { render, screen, within } from '@testing-library/react'
import { SyncLine } from './SyncLine'

function station(name: string) {
  return screen.getByText(name).closest('li')!
}

function pipeInto(name: string) {
  return station(name).querySelector('.pipe')!
}

describe('SyncLine', () => {
  it('shows how many orders are at each station', () => {
    render(<SyncLine counts={{ open: 2, queued: 1, synced: 148 }} />)

    expect(within(station('Abiertas')).getByText('2')).toBeInTheDocument()
    expect(within(station('En cola')).getByText('1')).toBeInTheDocument()
    expect(within(station('Sincronizadas')).getByText('148')).toBeInTheDocument()
  })

  it('makes the pipe flow into a station that just received orders', () => {
    const { rerender } = render(<SyncLine counts={{ open: 1, queued: 1, synced: 8 }} />)
    expect(pipeInto('Sincronizadas')).not.toHaveClass('pipe--flowing')

    rerender(<SyncLine counts={{ open: 1, queued: 0, synced: 9 }} />)

    expect(pipeInto('Sincronizadas')).toHaveClass('pipe--flowing')
    expect(pipeInto('En cola')).not.toHaveClass('pipe--flowing')
  })

  it('does not animate on the first load', () => {
    render(<SyncLine counts={{ open: 3, queued: 2, synced: 5 }} />)

    expect(pipeInto('En cola')).not.toHaveClass('pipe--flowing')
    expect(pipeInto('Sincronizadas')).not.toHaveClass('pipe--flowing')
  })
})
