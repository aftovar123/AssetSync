import { useEffect, useRef, useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import { useOperatorSession } from '../session/useOperatorSession'

const knownClients = [
  { id: 'erp-integration', does: 'Crea y completa órdenes de trabajo, y ve la sincronización.' },
  { id: 'asset-admin', does: 'Registra activos.' },
]

interface SignInDialogProps {
  open: boolean
  onClose: () => void
}

/**
 * Asks for a client's id and secret, the same pair Scalar asks for. The
 * secret is sent once to /auth/token and then dropped: only the token stays,
 * in memory.
 */
export function SignInDialog({ open, onClose }: SignInDialogProps) {
  const dialog = useRef<HTMLDialogElement>(null)
  const { signIn } = useOperatorSession()
  const [clientId, setClientId] = useState(knownClients[0].id)
  const [secret, setSecret] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  useEffect(() => {
    const element = dialog.current
    if (!element) return
    if (open && !element.open) element.showModal()
    if (!open && element.open) element.close()
  }, [open])

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setPending(true)
    setError(null)
    try {
      await signIn(clientId.trim(), secret.trim())
      setSecret('')
      onClose()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'No se pudo conectar con la API. Inténtalo de nuevo.')
    } finally {
      setPending(false)
    }
  }

  return (
    <dialog ref={dialog} className="dialog" onClose={onClose} aria-labelledby="sign-in-title">
      <form onSubmit={handleSubmit}>
        <h2 id="sign-in-title">Iniciar sesión como operador</h2>
        <p className="dialog__intro">
          Usa las credenciales de uno de los clientes configurados en la API. Cada uno tiene permisos
          distintos.
        </p>

        <label className="field">
          <span>Cliente</span>
          <select value={clientId} onChange={(event) => setClientId(event.target.value)}>
            {knownClients.map((client) => (
              <option key={client.id} value={client.id}>
                {client.id}
              </option>
            ))}
          </select>
          <small className="field__hint">{knownClients.find((client) => client.id === clientId)?.does}</small>
        </label>

        <label className="field">
          <span>Secreto del cliente</span>
          <input
            type="password"
            autoComplete="off"
            required
            value={secret}
            onChange={(event) => setSecret(event.target.value)}
          />
        </label>

        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}

        <div className="dialog__actions">
          <button type="button" onClick={onClose}>
            Cancelar
          </button>
          <button type="submit" className="button--primary" disabled={pending || secret.trim() === ''}>
            {pending ? 'Iniciando sesión…' : 'Iniciar sesión'}
          </button>
        </div>
      </form>
    </dialog>
  )
}
