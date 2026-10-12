import { useEffect, useState } from 'react'

// The free-tier Azure SQL database pauses when idle and takes up to a
// minute to wake up. A plain "loading" that long looks broken, so after a
// few seconds the message says why it is taking a while.
const SLOW_AFTER_MS = 4_000

export function Loading({ what }: { what: string }) {
  const [slow, setSlow] = useState(false)

  useEffect(() => {
    const timer = setTimeout(() => setSlow(true), SLOW_AFTER_MS)
    return () => clearTimeout(timer)
  }, [])

  return (
    <div className="loading" role="status">
      <span className="loading__bar" aria-hidden="true" />
      <p>Cargando {what}…</p>
      {slow && (
        <p className="loading__slow">
          La base de datos se pausa cuando no se usa y puede tardar hasta un minuto en responder la
          primera vez.
        </p>
      )}
    </div>
  )
}
