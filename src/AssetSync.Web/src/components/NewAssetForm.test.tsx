import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { mockFetch, renderWithOperator } from '../test-utils'
import { NewAssetForm } from './NewAssetForm'

afterEach(() => vi.restoreAllMocks())

describe('NewAssetForm', () => {
  it('registers the asset with its code in capitals and no empty location', async () => {
    const fetchMock = mockFetch({ status: 201, body: { id: 4001, code: 'BOMBA-002', name: 'Bomba de enfriamiento', location: null } })
    const onCreated = vi.fn()
    renderWithOperator(<NewAssetForm onCreated={onCreated} />, ['assets.write'])

    await userEvent.type(screen.getByLabelText('Código'), 'bomba-002')
    await userEvent.type(screen.getByLabelText('Nombre'), 'Bomba de enfriamiento')
    await userEvent.click(screen.getByRole('button', { name: 'Registrar activo' }))

    await vi.waitFor(() => expect(onCreated).toHaveBeenCalledWith('BOMBA-002'))
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/assets')
    expect(JSON.parse(String(init?.body))).toEqual({ code: 'BOMBA-002', name: 'Bomba de enfriamiento', location: null })
  })

  it('shows that the code is already taken', async () => {
    mockFetch({ status: 400, body: { errors: { Code: ['Ya existe un activo con el código BOMBA-001.'] } } })
    renderWithOperator(<NewAssetForm onCreated={vi.fn()} />, ['assets.write'])

    await userEvent.type(screen.getByLabelText('Código'), 'BOMBA-001')
    await userEvent.type(screen.getByLabelText('Nombre'), 'Otra bomba')
    await userEvent.click(screen.getByRole('button', { name: 'Registrar activo' }))

    expect(await screen.findByText('Ya existe un activo con el código BOMBA-001.')).toBeInTheDocument()
  })
})
