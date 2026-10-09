import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useOperatorSession } from '../session/useOperatorSession'
import { api, ApiError } from './client'

/**
 * Runs an operator action with the session token. A 401 means the token is
 * no longer accepted, so the session ends and the panel asks to sign in again.
 */
function useOperatorAction<TInput, TResult>(
  action: (accessToken: string, input: TInput) => Promise<TResult>,
  invalidates: string[],
) {
  const { session, signOut } = useOperatorSession()
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (input: TInput) => {
      if (!session) throw new ApiError(401, 'Inicia sesión como operador para hacer esto.')
      return action(session.accessToken, input)
    },
    onSuccess: () => Promise.all(invalidates.map((key) => queryClient.invalidateQueries({ queryKey: [key] }))),
    onError: (error) => {
      if (error instanceof ApiError && error.status === 401) signOut()
    },
  })
}

export function useCreateAsset() {
  return useOperatorAction(api.createAsset, ['assets'])
}

export function useCreateWorkOrder() {
  return useOperatorAction(api.createWorkOrder, ['work-orders'])
}

export function useCompleteWorkOrder() {
  return useOperatorAction(api.completeWorkOrder, ['work-orders', 'outbox'])
}
