import { useState } from 'react'
import { useAssets } from '../api/queries'
import { LoadError } from '../components/LoadError'
import { NewAssetForm } from '../components/NewAssetForm'
import { Pager } from '../components/Pager'
import { useOperatorSession } from '../session/useOperatorSession'

const PAGE_SIZE = 20

export function AssetsPage() {
  const [page, setPage] = useState(1)
  const assets = useAssets(page, PAGE_SIZE)
  const { can } = useOperatorSession()
  const [created, setCreated] = useState<string | null>(null)

  if (assets.isError) return <LoadError what="los activos" onRetry={() => assets.refetch()} />

  const data = assets.data

  return (
    <section className="panel" aria-labelledby="assets-title">
      <div className="panel__header">
        <h1 id="assets-title">Activos</h1>
      </div>
      {can('assets.write') && <NewAssetForm onCreated={setCreated} />}
      {created && (
        <p className="notice" role="status">
          Activo {created} registrado.
        </p>
      )}
      {data && data.items.length === 0 ? (
        <p className="empty">Todavía no hay activos registrados.</p>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Código</th>
              <th scope="col">Nombre</th>
              <th scope="col">Ubicación</th>
            </tr>
          </thead>
          <tbody>
            {data?.items.map((asset) => (
              <tr key={asset.id}>
                <td><span className="tag-code">{asset.code}</span></td>
                <td>{asset.name}</td>
                <td>{asset.location ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {data && (
        <Pager page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onChange={setPage} />
      )}
    </section>
  )
}
