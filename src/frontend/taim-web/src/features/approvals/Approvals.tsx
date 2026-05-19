import { useState, useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'
import { listPendingApprovals, listApprovalHistory, decideApproval, listAgents, type ApprovalResponse } from '../../lib/api'
import { onNotification } from '../../lib/signalr'
import { ApprovalQueue } from '../../components/ApprovalQueue/ApprovalQueue'
import type { ApprovalRequest, ApprovalScope } from '../../lib/types'

function toApprovalRequest(a: ApprovalResponse, nameMap: Record<string, string>): ApprovalRequest {
  return {
    id: a.id,
    agentId: a.agentId,
    agentName: nameMap[a.agentId] ?? a.agentId,
    toolName: a.toolName,
    toolDescription: a.description,
    parameters: a.toolArguments,
    status: a.status as ApprovalRequest['status'],
    requestedAt: a.createdAt,
  }
}

export function Approvals() {
  const [searchParams] = useSearchParams()
  const taskId = searchParams.get('taskId')

  const [tab, setTab] = useState<'pending' | 'history'>('pending')
  const [approvals, setApprovals] = useState<ApprovalRequest[]>([])
  const [history, setHistory] = useState<ApprovalResponse[]>([])
  const [nameMap, setNameMap] = useState<Record<string, string>>({})
  const [loading, setLoading] = useState(true)
  const [historyLoading, setHistoryLoading] = useState(false)
  const [deciding, setDeciding] = useState<string | null>(null)

  async function reload() {
    try {
      const [raw, agents] = await Promise.all([listPendingApprovals(), listAgents()])
      const map: Record<string, string> = {}
      for (const a of agents) map[a.id] = a.name
      setNameMap(map)
      setApprovals(raw.map(a => toApprovalRequest(a, map)))
    } catch {
      // silently ignore — may not be authenticated yet
    } finally {
      setLoading(false)
    }
  }

  async function loadHistory() {
    if (!taskId) return
    setHistoryLoading(true)
    try {
      const raw = await listApprovalHistory(taskId)
      setHistory(raw)
    } catch {
      // ignore
    } finally {
      setHistoryLoading(false)
    }
  }

  useEffect(() => { reload() }, [])

  useEffect(() => {
    if (tab === 'history') loadHistory()
  }, [tab, taskId])

  // Update in real-time when approval_required arrives
  useEffect(() => {
    return onNotification(n => {
      if (n.kind === 'approval_required') reload()
    })
  }, [])

  async function handleDecide(id: string, approved: boolean, scope: ApprovalScope) {
    setDeciding(id)
    try {
      await decideApproval(id, approved, scope)
      setApprovals(prev => prev.filter(a => a.id !== id))
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Failed to apply decision')
    } finally {
      setDeciding(null)
    }
  }

  return (
    <div style={styles.root}>
      <div style={styles.header}>
        <h1 style={styles.heading}>Approvals</h1>
      </div>

      <div style={styles.tabs}>
        <button
          style={{ ...styles.tab, ...(tab === 'pending' ? styles.tabActive : {}) }}
          onClick={() => setTab('pending')}
        >
          Pending {!loading && `(${approvals.length})`}
        </button>
        <button
          style={{ ...styles.tab, ...(tab === 'history' ? styles.tabActive : {}) }}
          onClick={() => setTab('history')}
        >
          History {!historyLoading && tab === 'history' ? `(${history.length})` : ''}
        </button>
      </div>

      {tab === 'pending' && (
        loading ? (
          <div style={styles.empty}>Loading…</div>
        ) : (
          <div style={{ opacity: deciding ? 0.7 : 1, transition: 'opacity 0.15s' }}>
            <ApprovalQueue requests={approvals} onDecide={handleDecide} />
          </div>
        )
      )}

      {tab === 'history' && (
        !taskId ? (
          <div style={styles.empty}>Open a task view and navigate to Approvals to see history for that task.</div>
        ) : historyLoading ? (
          <div style={styles.empty}>Loading…</div>
        ) : history.length === 0 ? (
          <div style={styles.empty}>No decided approvals for this task yet.</div>
        ) : (
          <div style={styles.historyList}>
            {history.map(a => (
              <HistoryRow key={a.id} approval={a} nameMap={nameMap} />
            ))}
          </div>
        )
      )}
    </div>
  )
}

function HistoryRow({ approval, nameMap }: { approval: ApprovalResponse; nameMap: Record<string, string> }) {
  const agentName = nameMap[approval.agentId] ?? approval.agentId.slice(0, 8)
  const approved = approval.status === 'approved'
  const decidedAt = approval.decidedAt
    ? new Date(approval.decidedAt).toLocaleString()
    : '—'

  return (
    <div style={styles.historyRow}>
      <div style={styles.historyRowMain}>
        <span style={styles.agentName}>{agentName}</span>
        <span style={styles.toolName}>{approval.toolName}</span>
        <span style={{ ...styles.decision, color: approved ? '#22c55e' : '#ef4444' }}>
          {approved ? '✓ Approved' : '✗ Denied'}
        </span>
        <span style={styles.scope}>{approval.scope}</span>
      </div>
      <div style={styles.historyRowMeta}>{decidedAt}</div>
    </div>
  )
}

const styles = {
  root: { maxWidth: 720, fontFamily: 'system-ui, sans-serif' },
  header: { display: 'flex', alignItems: 'center', gap: 12, marginBottom: 16 },
  heading: { fontSize: 20, fontWeight: 700, color: '#f1f5f9', margin: 0 },
  tabs: { display: 'flex', gap: 4, marginBottom: 20, borderBottom: '1px solid #1e293b', paddingBottom: 0 },
  tab: {
    background: 'none', border: 'none', borderBottom: '2px solid transparent',
    color: '#64748b', cursor: 'pointer', fontSize: 13, fontWeight: 500,
    padding: '6px 12px', marginBottom: -1, fontFamily: 'system-ui, sans-serif',
  },
  tabActive: { color: '#f1f5f9', borderBottomColor: '#60a5fa' },
  empty: { color: '#475569', fontSize: 14, padding: '2rem 0', textAlign: 'center' as const },
  historyList: { display: 'flex', flexDirection: 'column' as const, gap: 8 },
  historyRow: {
    background: '#1e293b', border: '1px solid #334155', borderRadius: 6,
    padding: '10px 14px',
  },
  historyRowMain: { display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' as const, marginBottom: 4 },
  historyRowMeta: { fontSize: 11, color: '#475569' },
  agentName: { fontSize: 13, fontWeight: 600, color: '#f1f5f9' },
  toolName: { fontSize: 12, color: '#94a3b8', background: '#0f172a', padding: '1px 6px', borderRadius: 4 },
  decision: { fontSize: 12, fontWeight: 600 },
  scope: { fontSize: 11, color: '#64748b' },
}
