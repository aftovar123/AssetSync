import type { ReactNode } from 'react'
import type { WorkOrder } from '../domain/models'
import { formatDateTime } from '../lib/format'
import { StageTag } from './StageTag'

interface WorkOrdersTableProps {
  orders: WorkOrder[]
  /** Asset code by id, so the table shows "BOMBA-001" instead of 2002. */
  assetCodes: Map<number, string>
  /** The full list also shows when each order was created. */
  showCreated?: boolean
  /** Per-row controls, such as completing the order; omitted when there are none. */
  actions?: (order: WorkOrder) => ReactNode
}

export function WorkOrdersTable({ orders, assetCodes, showCreated = false, actions }: WorkOrdersTableProps) {
  return (
    <table className="data-table">
      <thead>
        <tr>
          <th scope="col">Orden</th>
          <th scope="col">Activo</th>
          <th scope="col">Descripción</th>
          <th scope="col">Estado</th>
          {showCreated && <th scope="col">Creada</th>}
          <th scope="col">Completada</th>
          {actions && (
            <th scope="col">
              <span className="visually-hidden">Acciones</span>
            </th>
          )}
        </tr>
      </thead>
      <tbody>
        {orders.map((order) => (
          <tr key={order.id}>
            <td className="num">{order.id}</td>
            <td>
              <span className="tag-code">{assetCodes.get(order.assetId) ?? order.assetId}</span>
            </td>
            <td>{order.description}</td>
            <td>
              <StageTag order={order} />
            </td>
            {showCreated && <td className="when">{formatDateTime(order.createdAt)}</td>}
            <td className="when">{formatDateTime(order.completedAt)}</td>
            {actions && <td className="actions">{actions(order)}</td>}
          </tr>
        ))}
      </tbody>
    </table>
  )
}
