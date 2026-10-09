import { useState, type FormEvent } from 'react'
import { ValidationError } from '../api/client'
import { useCreateWorkOrder } from '../api/mutations'
import { useAssets } from '../api/queries'
import { FieldErrors } from './FieldErrors'

/**
 * Opens a work order on an asset. The asset is picked by its code, so the
 * operator never needs to know its numeric id.
 */
export function NewWorkOrderForm({ onCreated }: { onCreated: (id: number) => void }) {
  const assets = useAssets(1, 100)
  const createWorkOrder = useCreateWorkOrder()
  const [assetId, setAssetId] = useState('')
  const [description, setDescription] = useState('')

  const fieldErrors = createWorkOrder.error instanceof ValidationError ? createWorkOrder.error.fields : {}
  const generalError =
    createWorkOrder.error && !(createWorkOrder.error instanceof ValidationError) ? createWorkOrder.error.message : null

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    createWorkOrder.mutate(
      { assetId: Number(assetId), description: description.trim() },
      {
        onSuccess: (order) => {
          setDescription('')
          onCreated(order.id)
        },
      },
    )
  }

  return (
    <form className="inline-form" onSubmit={handleSubmit} aria-labelledby="new-order-title">
      <h2 id="new-order-title">Nueva orden de trabajo</h2>
      <div className="inline-form__fields">
        <label className="field">
          <span>Activo</span>
          <select required value={assetId} onChange={(event) => setAssetId(event.target.value)}>
            <option value="" disabled>
              Elige un activo
            </option>
            {assets.data?.items.map((asset) => (
              <option key={asset.id} value={asset.id}>
                {asset.code}: {asset.name}
              </option>
            ))}
          </select>
          <FieldErrors messages={fieldErrors.assetId} />
        </label>
        <label className="field field--wide">
          <span>Trabajo a realizar</span>
          <input
            required
            maxLength={500}
            value={description}
            placeholder="Por ejemplo: cambio de rodamientos"
            onChange={(event) => setDescription(event.target.value)}
          />
          <FieldErrors messages={fieldErrors.description} />
        </label>
      </div>
      {generalError && (
        <p className="form-error" role="alert">
          {generalError}
        </p>
      )}
      <button type="submit" className="button--primary" disabled={createWorkOrder.isPending}>
        {createWorkOrder.isPending ? 'Creando…' : 'Crear orden'}
      </button>
    </form>
  )
}
