export function LoadError({ what, onRetry }: { what: string; onRetry: () => void }) {
  return (
    <div className="load-error" role="alert">
      <p>
        No se pudieron cargar {what}. Revisa que la API esté en línea; si acaba de estar inactiva,
        puede tardar hasta un minuto en responder.
      </p>
      <button type="button" onClick={onRetry}>
        Reintentar
      </button>
    </div>
  )
}
