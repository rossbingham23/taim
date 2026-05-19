import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { listRootKpis } from '../../lib/api'
import { KpiDashboard } from '../../components/KpiDashboard/KpiDashboard'
import type { KpiNode } from '../../lib/types'

export function KpiPage() {
  const { taskId } = useParams<{ taskId: string }>()
  const navigate = useNavigate()
  const [kpis, setKpis] = useState<KpiNode[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!taskId) return
    setLoading(true)
    listRootKpis(taskId)
      .then(data => setKpis(data as unknown as KpiNode[]))
      .catch(err => setError(err instanceof Error ? err.message : 'Failed to load KPIs'))
      .finally(() => setLoading(false))
  }, [taskId])

  return (
    <div style={{ fontFamily: 'system-ui, sans-serif' }}>
      <div style={{ marginBottom: 24 }}>
        <button onClick={() => navigate(`/tasks/${taskId}`)} style={styles.back}>
          ← Back to Goal
        </button>
      </div>
      <h1 style={styles.title}>KPIs</h1>
      {loading && <div style={styles.muted}>Loading…</div>}
      {error && <div style={styles.error}>{error}</div>}
      {!loading && !error && kpis.length === 0 && (
        <div style={styles.muted}>No KPIs available.</div>
      )}
      {!loading && !error && kpis.length > 0 && (
        <KpiDashboard roots={kpis} />
      )}
    </div>
  )
}

const styles = {
  back: {
    background: 'none',
    border: 'none',
    color: '#64748b',
    cursor: 'pointer',
    fontSize: 13,
    padding: 0,
  } as const,
  title: {
    fontSize: 20,
    fontWeight: 700,
    color: '#f1f5f9',
    marginBottom: 20,
    marginTop: 0,
  } as const,
  muted: {
    color: '#64748b',
    fontSize: 14,
    padding: '2rem 0',
  } as const,
  error: {
    color: '#ef4444',
    fontSize: 13,
    padding: '1rem 0',
  } as const,
}
