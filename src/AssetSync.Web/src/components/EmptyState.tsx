import type { Scope } from '../domain/models'
import { useOperatorSession } from '../session/useOperatorSession'

interface EmptyStateProps {
  /** What is missing, as a full sentence: "Todavía no hay activos registrados." */
  message: string
  /** The first item, with its article: "el primer activo". */
  firstItem: string
  /** The scope that lets an operator create the first one. */
  createScope: Scope
  /** How a signed-in operator with that scope creates it. */
  createHint: string
  /** Which client has that scope, for operators signed in with another one. */
  createdBy: string
}

/** An empty list that tells each viewer how the first item gets there. */
export function EmptyState({ message, firstItem, createScope, createHint, createdBy }: EmptyStateProps) {
  const { session, can } = useOperatorSession()

  const next = can(createScope)
    ? createHint
    : session
      ? `Para crear ${firstItem}, inicia sesión como ${createdBy}.`
      : `Inicia sesión como operador para crear ${firstItem}.`

  return (
    <div className="empty">
      <p>{message}</p>
      <p className="empty__next">{next}</p>
    </div>
  )
}
