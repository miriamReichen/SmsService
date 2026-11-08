import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { api, ApiError, type Employee } from '../../core/api'

const MAX_BODY = 160

interface Props {
  onSent: () => void
}

export function SendMessageForm({ onSent }: Props) {
  const [employees, setEmployees] = useState<Employee[]>([])
  const [employeeId, setEmployeeId] = useState<number | ''>('')
  const [phoneId, setPhoneId] = useState<number | ''>('')
  const [body, setBody] = useState('')
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  // One key per logical send: a double click reuses it, so the server returns the same message.
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID())

  useEffect(() => {
    api.getEmployees()
      .then(setEmployees)
      .catch((e: Error) => setError(`טעינת עובדים נכשלה: ${e.message}`))
  }, [])

  const employee = useMemo(() => employees.find((e) => e.id === employeeId), [employees, employeeId])

  // A single phone is selected automatically (decision 33).
  useEffect(() => {
    setPhoneId(employee?.phones.length === 1 ? employee.phones[0].phoneId : '')
  }, [employee])

  const hasNoPhones = employee !== undefined && employee.phones.length === 0
  const canSend = phoneId !== '' && body.trim().length > 0 && body.length <= MAX_BODY && !sending

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!canSend) return // also narrows phoneId to number
    setSending(true)
    setError(null)
    setSuccess(null)
    try {
      const result = await api.sendMessage(phoneId, body, idempotencyKey)
      setSuccess(`ההודעה נקלטה (${result.status}). מזהה: ${result.id}`)
      setBody('')
      setIdempotencyKey(crypto.randomUUID())
      onSent()
    } catch (err) {
      const message = err instanceof ApiError ? `${err.code ?? 'ERROR'}: ${err.message}` : String(err)
      setError(message)
    } finally {
      setSending(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} style={{ display: 'grid', gap: 8, maxWidth: 480, marginBottom: 24 }}>
      <h2>שליחת הודעה</h2>

      <label>
        עובד{' '}
        <select
          value={employeeId}
          onChange={(e) => setEmployeeId(e.target.value === '' ? '' : Number(e.target.value))}
        >
          <option value="">בחרי עובד</option>
          {employees.map((emp) => (
            <option key={emp.id} value={emp.id}>
              {emp.firstName} {emp.lastName}, {emp.address}
            </option>
          ))}
        </select>
      </label>

      {employee && employee.phones.length > 1 && (
        <label>
          טלפון{' '}
          <select
            value={phoneId}
            onChange={(e) => setPhoneId(e.target.value === '' ? '' : Number(e.target.value))}
          >
            <option value="">בחרי טלפון</option>
            {employee.phones.map((p) => (
              <option key={p.phoneId} value={p.phoneId}>{p.number}</option>
            ))}
          </select>
        </label>
      )}

      {employee && employee.phones.length === 1 && <div>טלפון: {employee.phones[0].number}</div>}

      {hasNoPhones && <div role="alert">לעובד זה אין אף מספר טלפון, ולכן אי אפשר לשלוח אליו.</div>}

      <label>
        הודעה ({body.length}/{MAX_BODY}){' '}
        <textarea
          value={body}
          maxLength={MAX_BODY}
          rows={3}
          onChange={(e) => setBody(e.target.value)}
          style={{ width: '100%' }}
        />
      </label>

      <button type="submit" disabled={!canSend}>
        {sending ? 'שולח...' : 'שליחה'}
      </button>

      {error && <div role="alert" style={{ color: 'crimson' }}>{error}</div>}
      {success && <div role="status" style={{ color: 'green' }}>{success}</div>}
    </form>
  )
}
