import { useState, useEffect } from 'react'
import { listPendingApprovals, decideApproval, type ApprovalResponse } from '../../lib/api'
import { onNotification } from '../../lib/signalr'
import { ApprovalQueue } from '../../components/ApprovalQueue/ApprovalQueue'
import type { ApprovalRequest, ApprovalScope } from '../../lib/types'

function toApprovalRequest(a: ApprovalResponse): ApprovalRequest {
  return {
    id: a.id,
    agentId: a.agentId,
    agentName: a.agentId,   // resolved below if we have agent names
    toolName: a.toolName,
    toolDescription: a.description,
    parameters: a.toolArguments,
    status: a.status as ApprovalRequest['status'],
    requestedAt: a.createdAt,
  }
}

export function Approvals() {
  const [approvals, setApprovals] = useState<ApprovalRequest[]>([])
  const [loading, setLoading] = useState(true)
  const [deciding, setDeciding] = useState<string | null>(null)

  async function reload() {
    try {
      const raw = await listPendingApprovals()
      setApprovals(raw.map(toApprovalRequest))
    } catch {
      // silently ignore — may not be authenticated yet
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { reload() }, [])

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
        {!loading && (
          <span style={styles.badge}>
            {approvals.filter(a => a.status === 'pending').length} pending
          </span>
        )}
      </div>

      {loading ? (
        <div style={styles.empty}>Loading…</div>
      ) : (
        <div style={{ opacity: deciding ? 0.7 : 1, transition: 'opacity 0.15s' }}>
          <ApprovalQueue requests={approvals} onDecide={handleDecide} />
        </div>
      )}
    </div>
  )
}

const styles = {
  root: { maxWidth: 720, fontFamily: 'system-ui, sans-serif' },
  header: { display: 'flex', alignItems: 'center', gap: 12, marginBottom: 24 },
  heading: { fontSize: 20, fontWeight: 700, color: '#f1f5f9', margin: 0 },
  badge: {
    background: '#f59e0b22', color: '#f59e0b', fontSize: 12,
    fontWeight: 600, padding: '2px 10px', borderRadius: 999,
    border: '1px solid #f59e0b44',
  },
  empty: { color: '#475569', fontSize: 14, padding: '2rem 0', textAlign: 'center' as const },
}
