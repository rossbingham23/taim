import type { MeetingRecord, MeetingMessage, Agent } from '../../lib/types'

interface MeetingViewerProps {
  meeting: MeetingRecord
  messages: MeetingMessage[]
  agents: Agent[]
  onClose: () => void
}

const ROLE_COLORS: Record<string, string> = {
  ceo: '#f59e0b', cto: '#60a5fa', cmo: '#a78bfa', cfo: '#34d399',
  hr: '#fb923c', developer: '#22d3ee', designer: '#f472b6',
  qaEngineer: '#4ade80', productManager: '#facc15', marketingSpecialist: '#e879f9',
}

function getColor(role: string): string {
  return ROLE_COLORS[role] ?? '#94a3b8'
}

function meetingTypeLabel(t: string): string {
  return ({
    kickoff_sync: 'Kickoff Sync', status_check: 'Status Check',
    decision_request: 'Decision Request', escalation: 'Escalation', briefing: 'Briefing',
  })[t] ?? t
}

export function MeetingViewer({ meeting, messages, agents, onClose }: MeetingViewerProps) {
  const agentById = Object.fromEntries(agents.map(a => [a.id, a]))

  return (
    <div style={{ fontFamily: 'system-ui, sans-serif', background: '#0f172a', borderRadius: 8, overflow: 'hidden', border: '1px solid #334155' }}>
      <div style={{ padding: '12px 16px', background: '#1e293b', borderBottom: '1px solid #334155', display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between' }}>
        <div>
          <div style={{ fontWeight: 700, color: '#f1f5f9', fontSize: 14 }}>{meetingTypeLabel(meeting.meetingType)}</div>
          <div style={{ fontSize: 12, color: '#64748b', marginTop: 2 }}>{meeting.topic}</div>
          <div style={{ fontSize: 11, color: '#475569', marginTop: 4 }}>
            {messages.length} messages ·{' '}
            <span style={{ color: meeting.status === 'completed' ? '#22c55e' : meeting.status === 'failed' ? '#ef4444' : '#f59e0b' }}>
              {meeting.status === 'completed' ? 'Completed' : meeting.status === 'failed' ? 'Failed' : 'In Progress'}
            </span>
          </div>
        </div>
        <button onClick={onClose} style={{ background: 'none', border: 'none', color: '#64748b', cursor: 'pointer', fontSize: 16, padding: 0, lineHeight: 1 }}>✕</button>
      </div>

      <div style={{ maxHeight: 420, overflowY: 'auto', padding: '12px 16px', display: 'flex', flexDirection: 'column', gap: 12 }}>
        {messages.map(msg => {
          const agent = msg.speakerAgentId ? agentById[msg.speakerAgentId] : null
          const name = agent?.name ?? 'Unknown'
          const role = agent?.role ?? ''
          return (
            <div key={msg.id} style={{ display: 'flex', gap: 10 }}>
              <div style={{
                width: 30, height: 30, borderRadius: '50%', background: getColor(role),
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontSize: 11, fontWeight: 700, color: '#0f172a', flexShrink: 0,
              }}>
                {name.slice(0, 2).toUpperCase()}
              </div>
              <div style={{ flex: 1 }}>
                <div style={{ fontSize: 12, color: getColor(role), fontWeight: 600, marginBottom: 3 }}>
                  {name}
                  <span style={{ fontWeight: 400, color: '#475569', marginLeft: 8, fontSize: 11 }}>
                    {new Date(msg.createdAt).toLocaleTimeString()}
                  </span>
                </div>
                <div style={{ fontSize: 13, color: '#cbd5e1', lineHeight: 1.55 }}>{msg.content}</div>
              </div>
            </div>
          )
        })}
        {messages.length === 0 && (
          <div style={{ color: '#475569', fontSize: 13, textAlign: 'center', padding: '1rem' }}>No messages yet.</div>
        )}
      </div>

      {meeting.summary && (
        <div style={{ padding: '10px 16px', background: '#1e293b', borderTop: '1px solid #334155' }}>
          <div style={{ fontSize: 11, color: '#64748b', fontWeight: 600, marginBottom: 4, textTransform: 'uppercase', letterSpacing: 0.5 }}>Summary</div>
          <div style={{ fontSize: 12, color: '#94a3b8', lineHeight: 1.5 }}>{meeting.summary}</div>
        </div>
      )}
    </div>
  )
}
