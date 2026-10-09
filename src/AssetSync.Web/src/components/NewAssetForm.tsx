import { useState, type FormEvent } from 'react'
import { ValidationError } from '../api/client'
import { useCreateAsset } from '../api/mutations'
import { FieldErrors } from './FieldErrors'

/** Registers an asset under the code stamped on the equipment, such as BOMBA-002. */
export function NewAssetForm({ onCreated }: { onCreated: (code: string) => void }) {
  const createAsset = useCreateAsset()
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [location, setLocation] = useState('')

  const fieldErrors = createAsset.error instanceof ValidationError ? createAsset.error.fields : {}
  const generalError =
    createAsset.error && !(createAsset.error instanceof ValidationError) ? createAsset.error.message : null

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    createAsset.mutate(
      { code: code.trim().toUpperCase(), name: name.trim(), location: location.trim() || null },
      {
        onSuccess: (asset) => {
          setCode('')
          setName('')
          setLocation('')
          onCreated(asset.code)
        },
      },
    )
  }

  return (
    <form className="inline-form" onSubmit={handleSubmit} aria-labelledby="new-asset-title">
      <h2 id="new-asset-title">Nuevo activo</h2>
      <div className="inline-form__fields">
        <label className="field">
          <span>Código</span>
          <input
            required
            maxLength={50}
            value={code}
            placeholder="BOMBA-002"
            onChange={(event) => setCode(event.target.value)}
          />
          <FieldErrors messages={fieldErrors.code} />
        </label>
        <label className="field field--wide">
          <span>Nombre</span>
          <input
            required
            maxLength={200}
            value={name}
            placeholder="Bomba de agua de enfriamiento"
            onChange={(event) => setName(event.target.value)}
          />
          <FieldErrors messages={fieldErrors.name} />
        </label>
        <label className="field">
          <span>Ubicación (opcional)</span>
          <input maxLength={200} value={location} onChange={(event) => setLocation(event.target.value)} />
          <FieldErrors messages={fieldErrors.location} />
        </label>
      </div>
      {generalError && (
        <p className="form-error" role="alert">
          {generalError}
        </p>
      )}
      <button type="submit" className="button--primary" disabled={createAsset.isPending}>
        {createAsset.isPending ? 'Registrando…' : 'Registrar activo'}
      </button>
    </form>
  )
}
