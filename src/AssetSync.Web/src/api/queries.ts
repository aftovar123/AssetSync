import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useOperatorSession } from '../session/useOperatorSession'
import { api } from './client'

// The outbox processor runs every 10 seconds, so polling the work orders
// every 5 lets an operator watch an order turn "synced" without reloading.
const WORK_ORDER_POLL_MS = 5_000
const HEALTH_POLL_MS = 30_000

export function useHealth() {
  return useQuery({
    queryKey: ['health'],
    queryFn: ({ signal }) => api.health(signal),
    refetchInterval: HEALTH_POLL_MS,
  })
}

export function useAssets(page: number, pageSize: number) {
  return useQuery({
    queryKey: ['assets', page, pageSize],
    queryFn: ({ signal }) => api.assets(page, pageSize, signal),
    placeholderData: keepPreviousData,
  })
}

export function useWorkOrders(page: number, pageSize: number) {
  return useQuery({
    queryKey: ['work-orders', page, pageSize],
    queryFn: ({ signal }) => api.workOrders(page, pageSize, signal),
    placeholderData: keepPreviousData,
    refetchInterval: WORK_ORDER_POLL_MS,
  })
}

/** Asset codes by id, to show "BOMBA-001" next to a work order instead of 2002. */
export function useAssetCodes() {
  const { data } = useAssets(1, 100)
  return new Map(data?.items.map((asset) => [asset.id, asset.code]))
}

/** The outbox, for operators whose token can read the integration state. */
export function useOutbox(page: number, pageSize: number) {
  const { session, can } = useOperatorSession()
  return useQuery({
    queryKey: ['outbox', page, pageSize, session?.clientId],
    queryFn: ({ signal }) => api.outbox(session!.accessToken, page, pageSize, signal),
    enabled: can('integration.read'),
    placeholderData: keepPreviousData,
    refetchInterval: WORK_ORDER_POLL_MS,
  })
}

/** Every attempt to send one work order to the ERP. */
export function useIntegrationLogs(workOrderId: number | null) {
  const { session, can } = useOperatorSession()
  return useQuery({
    queryKey: ['integration-logs', workOrderId, session?.clientId],
    queryFn: ({ signal }) => api.integrationLogs(session!.accessToken, workOrderId!, signal),
    enabled: workOrderId !== null && can('integration.read'),
  })
}
