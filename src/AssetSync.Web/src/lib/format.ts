const dateTime = new Intl.DateTimeFormat('es-CO', {
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
})

/** "5 oct, 15:31" in the viewer's own time zone. The API always sends UTC. */
export function formatDateTime(iso: string | null): string {
  return iso ? dateTime.format(new Date(iso)) : '—'
}

const count = new Intl.NumberFormat('es-CO')

export function formatCount(value: number): string {
  return count.format(value)
}
