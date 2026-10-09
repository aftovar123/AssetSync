import { useEffect, useState } from 'react'
import { formatCount } from '../lib/format'
import type { SyncLineCounts } from '../domain/sync'

interface Station {
  key: keyof SyncLineCounts
  name: string
  detail: string
}

const stations: Station[] = [
  { key: 'open', name: 'Abiertas', detail: 'En trabajo de mantenimiento' },
  { key: 'queued', name: 'En cola', detail: 'Completadas, esperando el envío al ERP' },
  { key: 'synced', name: 'Sincronizadas', detail: 'Confirmadas por el ERP' },
]

/**
 * The work orders' path to the ERP drawn as a process line. When an order
 * moves between stations, the pipe it travelled through flows once, so a
 * sync happening in the background is visible the moment it lands.
 */
export function SyncLine({ counts, scopeNote }: { counts: SyncLineCounts; scopeNote?: string }) {
  const flowing = useChangedSegments(counts)

  return (
    <section className="sync-line" aria-labelledby="sync-line-title">
      <div className="sync-line__heading">
        <h2 id="sync-line-title">Camino al ERP</h2>
        {scopeNote && <p className="sync-line__note">{scopeNote}</p>}
      </div>
      <ol className="sync-line__track">
        {stations.map((station, index) => (
          <li key={station.key} className={`station station--${station.key}`}>
            {index > 0 && (
              <span
                className={`pipe${flowing.has(index) ? ' pipe--flowing' : ''}`}
                aria-hidden="true"
              />
            )}
            <span className="station__vessel">
              <span className="station__count">{formatCount(counts[station.key])}</span>
            </span>
            <span className="station__name">{station.name}</span>
            <span className="station__detail">{station.detail}</span>
          </li>
        ))}
      </ol>
    </section>
  )
}

/**
 * Pipe i sits between station i-1 and station i. It flows when station i
 * gained orders since the previous counts: something just arrived through it.
 * The comparison happens during render (React's pattern for reacting to a
 * prop change); the effect only turns the flow off again.
 */
function useChangedSegments(counts: SyncLineCounts): Set<number> {
  const [previous, setPrevious] = useState(counts)
  const [flowing, setFlowing] = useState<Set<number>>(() => new Set())

  if (stations.some((station) => counts[station.key] !== previous[station.key])) {
    const changed = new Set<number>()
    stations.forEach((station, index) => {
      if (index > 0 && counts[station.key] > previous[station.key]) changed.add(index)
    })
    setPrevious(counts)
    setFlowing(changed)
  }

  useEffect(() => {
    if (flowing.size === 0) return
    const timer = setTimeout(() => setFlowing(new Set()), 1600)
    return () => clearTimeout(timer)
  }, [flowing])

  return flowing
}
