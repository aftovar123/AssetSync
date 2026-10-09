import { createContext } from 'react'
import type { OperatorSession, Scope } from '../domain/models'

export interface OperatorSessionValue {
  session: OperatorSession | null
  signIn: (clientId: string, clientSecret: string) => Promise<void>
  signOut: () => void
  /** Whether the signed-in operator's token carries this scope. */
  can: (scope: Scope) => boolean
}

export const OperatorSessionContext = createContext<OperatorSessionValue | null>(null)
