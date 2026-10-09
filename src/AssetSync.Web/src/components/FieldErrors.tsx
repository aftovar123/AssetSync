/** The API's validation messages for one field, shown right under it. */
export function FieldErrors({ messages }: { messages?: string[] }) {
  if (!messages?.length) return null
  return <small className="field__error">{messages.join(' ')}</small>
}
