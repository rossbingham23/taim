import type { Agent } from '../../lib/types'

const STATUS_COLORS: Record<Agent['status'], string> = {
  idle: '#94a3b8',
  active: '#22c55e',
  waiting_approval: '#f59e0b',
  sleeping: '#6366f1',
  terminated: '#ef4444',
}

const STATUS_LABELS: Record<Agent['status'], string> = {
  idle: 'Idle',
  active: 'Active',
  waiting_approval: 'Waiting Approval',
  sleeping: 'Sleeping',
  terminated: 'Terminated',
}

interface AgentCardProps {
  agent: Agent
  onClick?: (agent: Agent) => void
}

export function AgentCard({ agent, onClick }: AgentCardProps) {
  const statusColor = STATUS_COLORS[agent.status]

  return (
    <div
      onClick={() => onClick?.(agent)}
      style={{
        border: `2px solid ${statusColor}`,
        borderRadius: 8,
        padding: '1rem',
        cursor: onClick ? 'pointer' : 'default',
        background: '#1e293b',
        color: '#f1f5f9',
        minWidth: 200,
        fontFamily: 'sans-serif',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
        <span
          style={{
            width: 10,
            height: 10,
            borderRadius: '50%',
            background: statusColor,
            display: 'inline-block',
            flexShrink: 0,
          }}
        />
        <span style={{ fontWeight: 700, fontSize: 14 }}>{agent.name}</span>
      </div>
      <div style={{ fontSize: 12, color: '#94a3b8', marginBottom: 4 }}>{agent.role.toUpperCase()}</div>
      <div style={{ fontSize: 12, color: statusColor, marginBottom: 8 }}>{STATUS_LABELS[agent.status]}</div>
      {agent.currentTask && (
        <div style={{ fontSize: 11, color: '#cbd5e1', borderTop: '1px solid #334155', paddingTop: 8 }}>
          {agent.currentTask}
        </div>
      )}
      <div style={{ fontSize: 10, color: '#64748b', marginTop: 8 }}>
        {agent.provider} / {agent.model}
      </div>
    </div>
  )
}
