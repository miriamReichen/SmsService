import { useState } from 'react'
import { SendMessageForm } from './features/messages/SendMessageForm'
import { MessagesTable } from './features/messages/MessagesTable'

export default function App() {
  // Bumped after a successful send so the table refreshes immediately.
  const [refreshKey, setRefreshKey] = useState(0)

  return (
    <main style={{ fontFamily: 'sans-serif', maxWidth: 1000, margin: '0 auto', padding: 16 }}>
      <h1>שליחת SMS לעובדים</h1>
      <SendMessageForm onSent={() => setRefreshKey((k) => k + 1)} />
      <MessagesTable refreshKey={refreshKey} />
    </main>
  )
}
