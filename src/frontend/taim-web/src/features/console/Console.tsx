import { useState, useEffect } from 'react'
import { listActivity } from '../../lib/api'
import { onNotification } from '../../lib/signalr'
import type { Notification } from '../../lib/types'
import { ActivityConsole } from '../../components/ActivityConsole/ActivityConsole'

export function Console() {
  const [entries, setEntries] = useState<Notification[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    listActivity(undefined, 500)
      .then(setEntries)
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    return onNotification(n => {
      setEntries(prev => {
        if (prev.some(e => e.id === n.id)) return prev
        return [...prev, n]
      })
    })
  }, [])

  return (
    <div style={styles.root}>
      <div style={styles.header}>
        <h1 style={styles.heading}>System Console</h1>
        <button style={styles.clearBtn} onClick={() => setEntries([])}>Clear</button>
      </div>
      <p style={styles.intro}>
        Live feed of all system events — agent activations, KPI proposals, strategy reports, approvals.
        Updates in real time via SignalR.
      </p>
      {loading ? (
        <div style={styles.loading}>Loading…</div>
      ) : (
        <ActivityConsole entries={entries} title="All Activity" maxHeight={600} />
      )}
    </div>
  )
}

const styles = {
  root: { fontFamily: 'system-ui, sans-serif', maxWidth: 900 },
  header: { display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 },
  heading: { fontSize: 20, fontWeight: 700, color: '#f1f5f9', margin: 0 },
  clearBtn: {
    background: 'none', border: '1px solid #334155', borderRadius: 4,
    color: '#64748b', cursor: 'pointer', fontSize: 12, padding: '4px 10px',
    fontFamily: 'system-ui, sans-serif',
  },
  intro: { fontSize: 14, color: '#94a3b8', marginBottom: 20, lineHeight: 1.6 },
  loading: { color: '#475569', fontSize: 14 },
}
