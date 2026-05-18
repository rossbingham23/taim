import type { ApprovalRequest, ApprovalScope } from '../../lib/types'

interface ApprovalQueueProps {
  requests: ApprovalRequest[]
  onDecide: (id: string, approved: boolean, scope: ApprovalScope) => void
}

const SCOPE_LABELS: Record<ApprovalScope, string> = {
  once: 'Approve once',
  agent_and_tool: 'Always allow this agent to use this tool',
  agent_tool_and_param: 'Always allow with these exact parameters',
}

function ApprovalItem({ request, onDecide }: { request: ApprovalRequest; onDecide: ApprovalQueueProps['onDecide'] }) {
  return (
    <div style={{ border: '1px solid #334155', borderRadius: 8, padding: '1rem', marginBottom: 12, background: '#0f172a', fontFamily: 'sans-serif' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 8 }}>
        <div>
          <span style={{ fontWeight: 700, color: '#f59e0b', fontSize: 14 }}>{request.agentName}</span>
          <span style={{ color: '#94a3b8', fontSize: 14 }}> wants to call </span>
          <span style={{ fontWeight: 700, color: '#60a5fa', fontSize: 14, fontFamily: 'monospace' }}>{request.toolName}</span>
        </div>
        <div style={{ fontSize: 11, color: '#64748b' }}>{new Date(request.requestedAt).toLocaleTimeString()}</div>
      </div>
      {Object.keys(request.parameters).length > 0 && (
        <pre style={{ background: '#1e293b', borderRadius: 4, padding: 8, fontSize: 11, color: '#cbd5e1', overflow: 'auto', marginBottom: 12 }}>
          {JSON.stringify(request.parameters, null, 2)}
        </pre>
      )}
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        {(['once', 'agent_and_tool', 'agent_tool_and_param'] as ApprovalScope[]).map(scope => (
          <button
            key={scope}
            onClick={() => onDecide(request.id, true, scope)}
            style={{
              padding: '4px 12px', borderRadius: 4, border: '1px solid #22c55e',
              background: 'transparent', color: '#22c55e', cursor: 'pointer', fontSize: 12,
            }}
          >
            {SCOPE_LABELS[scope]}
          </button>
        ))}
        <button
          onClick={() => onDecide(request.id, false, 'once')}
          style={{
            padding: '4px 12px', borderRadius: 4, border: '1px solid #ef4444',
            background: 'transparent', color: '#ef4444', cursor: 'pointer', fontSize: 12, marginLeft: 'auto',
          }}
        >
          Deny
        </button>
      </div>
    </div>
  )
}

export function ApprovalQueue({ requests, onDecide }: ApprovalQueueProps) {
  const pending = requests.filter(r => r.status === 'pending')

  if (pending.length === 0) {
    return (
      <div style={{ padding: '2rem', color: '#475569', fontFamily: 'sans-serif', textAlign: 'center' }}>
        No pending approvals.
      </div>
    )
  }

  return (
    <div>
      <div style={{ fontFamily: 'sans-serif', fontSize: 13, color: '#94a3b8', marginBottom: 12 }}>
        {pending.length} pending {pending.length === 1 ? 'approval' : 'approvals'}
      </div>
      {pending.map(r => (
        <ApprovalItem key={r.id} request={r} onDecide={onDecide} />
      ))}
    </div>
  )
}
