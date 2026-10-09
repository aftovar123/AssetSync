interface PagerProps {
  page: number
  totalPages: number
  totalCount: number
  onChange: (page: number) => void
}

export function Pager({ page, totalPages, totalCount, onChange }: PagerProps) {
  if (totalPages <= 1) return null

  return (
    <nav className="pager" aria-label="Páginas">
      <button type="button" onClick={() => onChange(page - 1)} disabled={page <= 1}>
        Anterior
      </button>
      <span className="pager__position">
        Página {page} de {totalPages}, {totalCount} en total
      </span>
      <button type="button" onClick={() => onChange(page + 1)} disabled={page >= totalPages}>
        Siguiente
      </button>
    </nav>
  )
}
