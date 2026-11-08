import { useCallback, useEffect, useState } from 'react'
import { api, MESSAGE_STATUSES, type Message, type MessageStatus } from '../../core/api'

const REFRESH_MS = 2000

interface Props {
  refreshKey: number
}

export function MessagesTable({ refreshKey }: Props) {
  const [messages, setMessages] = useState<Message[]>([])
  const [status, setStatus] = useState<MessageStatus | ''>('')
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(() => {
    api.getMessages(status || undefined)
      .then((data) => {
        setMessages(data)
        setError(null)
      })
      .catch((e: Error) => setError(`טעינת הודעות נכשלה: ${e.message}`))
  }, [status])

  // Refresh on filter change, after a send, and every 2 seconds to show status changes.
  useEffect(() => {
    load()
    const timer = setInterval(load, REFRESH_MS)
    return () => clearInterval(timer)
  }, [load, refreshKey])

  return (
    <section>
      <h2>הודעות</h2>
      <label>
        סינון לפי סטטוס{' '}
        <select value={status} onChange={(e) => setStatus(e.target.value as MessageStatus | '')}>
          <option value="">הכול</option>
          {MESSAGE_STATUSES.map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
      </label>

      {error && <div role="alert" style={{ color: 'crimson' }}>{error}</div>}

      <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: 8 }} border={1} cellPadding={4}>
        <caption style={{ textAlign: 'start' }}>עד 100 הודעות אחרונות, מהחדשה לישנה</caption>
        <thead>
          <tr>
            <th scope="col">נוצרה</th>
            <th scope="col">עובד</th>
            <th scope="col">טלפון</th>
            <th scope="col">הודעה</th>
            <th scope="col">סטטוס</th>
            <th scope="col">סיבת כשל</th>
          </tr>
        </thead>
        <tbody>
          {messages.length === 0 && (
            <tr><td colSpan={6}>אין הודעות</td></tr>
          )}
          {messages.map((m) => (
            <tr key={m.id}>
              <td>{new Date(m.createdAt).toLocaleString('he-IL')}</td>
              <td>{m.employee.firstName} {m.employee.lastName}, {m.employee.address}</td>
              <td dir="ltr">{m.phoneNumber}</td>
              <td>{m.body}</td>
              <td>{m.status}</td>
              <td>{m.failureReason ?? ''}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  )
}
