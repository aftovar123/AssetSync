import { NavLink, Outlet } from 'react-router-dom'
import { useHealth } from '../api/queries'
import type { HealthStatus } from '../domain/models'
import { OperatorStatus } from './OperatorStatus'

const lampText: Record<HealthStatus | 'unknown', string> = {
  Healthy: 'API en línea',
  Degraded: 'API en línea con avisos',
  Unhealthy: 'API con fallas',
  unknown: 'Conectando con la API',
}

export function Layout() {
  const health = useHealth()
  const status: HealthStatus | 'unknown' = health.isError ? 'Unhealthy' : (health.data?.status ?? 'unknown')

  return (
    <div className="shell">
      <header className="masthead">
        <div className="masthead__brand">
          <span className="wordmark">AssetSync</span>
          <span className="masthead__tagline">Mantenimiento de activos e integración con el ERP</span>
        </div>
        <nav className="masthead__nav" aria-label="Secciones">
          <NavLink to="/" end>Resumen</NavLink>
          <NavLink to="/ordenes">Órdenes de trabajo</NavLink>
          <NavLink to="/activos">Activos</NavLink>
          <NavLink to="/cola">Cola de sincronización</NavLink>
        </nav>
        <div className="masthead__side">
          <p className={`lamp lamp--${status.toLowerCase()}`} role="status">
            <span className="lamp__light" aria-hidden="true" />
            {lampText[status]}
          </p>
          <OperatorStatus />
        </div>
      </header>
      <main className="content">
        <Outlet />
      </main>
    </div>
  )
}
