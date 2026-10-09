import { useState } from 'react'
import type { Scope } from '../domain/models'
import { useOperatorSession } from '../session/useOperatorSession'
import { SignInDialog } from './SignInDialog'

const scopeNames: Record<Scope, string> = {
  'assets.write': 'registrar activos',
  'workorders.write': 'crear y completar órdenes',
  'integration.read': 'ver la sincronización',
}

/** Who is operating the panel and what they may do, or the way to sign in. */
export function OperatorStatus() {
  const { session, signOut } = useOperatorSession()
  const [signingIn, setSigningIn] = useState(false)

  if (!session) {
    return (
      <>
        <button type="button" className="button--primary" onClick={() => setSigningIn(true)}>
          Iniciar sesión como operador
        </button>
        <SignInDialog open={signingIn} onClose={() => setSigningIn(false)} />
      </>
    )
  }

  return (
    <div className="operator">
      <p className="operator__who">
        <strong>{session.clientId}</strong>
        <span className="operator__can">Puede {session.scopes.map((scope) => scopeNames[scope] ?? scope).join(' y ')}</span>
      </p>
      <button type="button" onClick={signOut}>
        Cerrar sesión
      </button>
    </div>
  )
}
