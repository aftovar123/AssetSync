import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api } from '../api/client'
import type { OperatorSession, Scope } from '../domain/models'
import { OperatorSessionContext } from './context'

/**
 * Holds the operator's token in React state only: never in localStorage or
 * a cookie, so closing or reloading the tab signs the operator out and no
 * script on the page can read a stored credential. The session also ends by
 * itself when the token expires.
 */
export function OperatorSessionProvider({
  children,
  initialSession = null,
}: {
  children: ReactNode
  /** Starts already signed in; used by tests. */
  initialSession?: OperatorSession | null
}) {
  const [session, setSession] = useState<OperatorSession | null>(initialSession)

  const signIn = useCallback(async (clientId: string, clientSecret: string) => {
    const token = await api.requestToken(clientId, clientSecret)
    setSession({
      clientId,
      scopes: token.scopes,
      accessToken: token.accessToken,
      expiresAt: Date.now() + token.expiresInSeconds * 1000,
    })
  }, [])

  const signOut = useCallback(() => setSession(null), [])

  useEffect(() => {
    if (!session) return
    const timer = setTimeout(() => setSession(null), session.expiresAt - Date.now())
    return () => clearTimeout(timer)
  }, [session])

  const value = useMemo(
    () => ({
      session,
      signIn,
      signOut,
      can: (scope: Scope) => session?.scopes.includes(scope) ?? false,
    }),
    [session, signIn, signOut],
  )

  return <OperatorSessionContext.Provider value={value}>{children}</OperatorSessionContext.Provider>
}
