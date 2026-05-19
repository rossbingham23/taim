import { useState, useEffect, useCallback } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { getTask, listAgents, listActivity, listActions, listMeetings, getMeeting, executeAction, terminateTask, type AgentResponse, type TeamGraphResponse } from '../../lib/api'
import { onNotification } from '../../lib/signalr'
import { TeamGraph } from '../../components/TeamGraph/TeamGraph'
import { AgentCard } from '../../components/AgentCard/AgentCard'
import { BudgetMeter } from '../../components/BudgetMeter/BudgetMeter'
import { ActivityConsole } from '../../components/ActivityConsole/ActivityConsole'
import { MeetingViewer } from '../../components/MeetingViewer/MeetingViewer'
import type { Agent, TeamGraph as TeamGraphType, BudgetInfo, Notification, ActionItem, MeetingRecord, MeetingMessage } from '../../lib/types'

function toAgentType(a: AgentResponse): Agent {
  return {
    id: a.id,
    name: a.name,
    role: a.role as Agent['role'],
    status: a.status as Agent['status'],
    parentId: a.parentAgentId,
    provider: a.provider ?? 'unknown',
    model: a.model ?? 'unknown',
    createdAt: a.createdAt,
  }
}

function toGraphType(g: TeamGraphResponse): TeamGraphType {
  return {
    taskId: g.taskId,
    nodes: g.nodes.map(n => ({
      agentId: n.agentId,
      name: n.name,
      role: n.role as Agent['role'],
      status: n.status as Agent['status'],
    })),
    edges: g.edges.map(e => ({
      fromAgentId: e.parentAgentId,
      toAgentId: e.childAgentId,
    })),
  }
}

export function TeamView() {
  const { taskId } = useParams<{ taskId: string }>()
  const navigate = useNavigate()

  const [goal, setGoal] = useState<string>('')
  const [status, setStatus] = useState<string>('')
  const [graph, setGraph] = useState<TeamGraphType | null>(null)
  const [agents, setAgents] = useState<Agent[]>([])
  const [selected, setSelected] = useState<Agent | null>(null)
  const [budget] = useState<BudgetInfo | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [activityEntries, setActivityEntries] = useState<Notification[]>([])
  const [actions, setActions] = useState<ActionItem[]>([])
  const [showConsole, setShowConsole] = useState(true)
  const [meetings, setMeetings] = useState<MeetingRecord[]>([])
  const [selectedMeeting, setSelectedMeeting] = useState<{ meeting: MeetingRecord; messages: MeetingMessage[] } | null>(null)
  const [runningActionId, setRunningActionId] = useState<string | null>(null)
  const [terminating, setTerminating] = useState(false)

  const reload = useCallback(async () => {
    if (!taskId) return
    try {
      const [taskRes, agentList, activity, actionList, meetingList] = await Promise.all([
        getTask(taskId),
        listAgents(),
        listActivity(taskId, 200),
        listActions(taskId),
        listMeetings(taskId),
      ])
      setGoal(taskRes.task.goal)
      setStatus(taskRes.task.status)
      const g = toGraphType(taskRes.graph)
      setGraph(g)
      // Only show agents that belong to this task's graph
      const taskNodeIds = new Set(g.nodes.map(n => n.agentId))
      setAgents(agentList.filter(a => taskNodeIds.has(a.id)).map(toAgentType))
      // Merge activity entries (deduplicate by id)
      setActivityEntries(prev => {
        const existingIds = new Set(prev.map(e => e.id))
        const newEntries = activity.filter(e => !existingIds.has(e.id))
        return newEntries.length > 0 ? [...prev, ...newEntries] : prev
      })
      setActions(actionList)
      setMeetings(meetingList)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load task')
    } finally {
      setLoading(false)
    }
  }, [taskId])

  useEffect(() => { reload() }, [reload])

  // Append new task-scoped events via SignalR
  useEffect(() => {
    return onNotification(n => {
      if (n.metadata?.taskId === taskId) {
        setActivityEntries(prev => prev.some(e => e.id === n.id) ? prev : [...prev, n])
      }
    })
  }, [taskId])

  // Refresh on SignalR team update, action, or meeting events
  useEffect(() => {
    return onNotification(n => {
      if (n.kind === 'team_update' || n.kind === 'agent_status_changed' ||
          n.kind === 'action_created' || n.kind === 'action_updated' ||
          n.kind === 'meeting_completed') reload()
    })
  }, [reload])

  // Poll while bootstrapping
  useEffect(() => {
    if (status !== 'bootstrapping') return
    const id = setInterval(reload, 3000)
    return () => clearInterval(id)
  }, [status, reload])

  const openMeeting = async (meetingId: string) => {
    try {
      const data = await getMeeting(meetingId)
      setSelectedMeeting(data)
    } catch { /* ignore */ }
  }

  const handleTerminate = async () => {
    if (!taskId) return
    if (!window.confirm('Terminate this task? All running agents and actions will be stopped and cancelled.')) return
    setTerminating(true)
    try {
      await terminateTask(taskId)
      navigate('/')
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Failed to terminate task')
      setTerminating(false)
    }
  }

  const handleRunAction = async (actionId: string) => {
    setRunningActionId(actionId)
    try {
      await executeAction(actionId)
    } catch { /* SignalR action_updated will update status */ }
    finally {
      setRunningActionId(null)
    }
  }

  if (loading) return <Spinner />
  if (error) return <ErrorMsg msg={error} />

  return (
    <div style={styles.root}>
      {/* Header */}
      <div style={styles.header}>
        <button onClick={() => navigate('/')} style={styles.back}>← Back</button>
        <div style={{ flex: 1 }}>
          <div style={styles.goalText}>{goal}</div>
          <StatusBadge status={status} />
        </div>
        {status === 'bootstrapping' && <Spinner inline />}
        <Link to={`/tasks/${taskId}/kpis`} style={styles.kpisLink}>KPIs ↗</Link>
        {(status === 'active' || status === 'bootstrapping') && (
          <button
            onClick={handleTerminate}
            disabled={terminating}
            style={styles.terminateBtn}
          >
            {terminating ? 'Terminating…' : 'Terminate ✕'}
          </button>
        )}
      </div>

      <div style={styles.body}>
        {/* Left: org chart + agents */}
        <div style={styles.main}>
          {/* Only show the graph when there's actual hierarchy (edges between agents) */}
          {graph && graph.edges.length > 0 && (
            <section style={styles.section}>
              <h2 style={styles.sectionTitle}>Team Structure</h2>
              <TeamGraph
                graph={graph}
                onNodeClick={node => setSelected(agents.find(a => a.id === node.agentId) ?? null)}
              />
            </section>
          )}

          {agents.length > 0 && (
            <section style={styles.section}>
              <h2 style={styles.sectionTitle}>Team ({agents.length})</h2>
              <div style={styles.agentGrid}>
                {agents.map(agent => (
                  <AgentCard key={agent.id} agent={agent} onClick={setSelected} />
                ))}
              </div>
            </section>
          )}

          {status === 'bootstrapping' && agents.length === 0 && (
            <div style={styles.bootstrapping}>
              <div style={styles.pulse} />
              Assembling your team…
            </div>
          )}

          {/* Activity console */}
          <section style={styles.section}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12 }}>
              <h2 style={{ ...styles.sectionTitle, marginBottom: 0 }}>Activity</h2>
              <button
                onClick={() => setShowConsole(v => !v)}
                style={styles.toggleBtn}
              >
                {showConsole ? '▼' : '▶'}
              </button>
            </div>
            {showConsole && (
              <ActivityConsole entries={activityEntries} title="" maxHeight={280} />
            )}
          </section>

          {/* Meetings */}
          {meetings.length > 0 && (
            <section style={styles.section}>
              <h2 style={styles.sectionTitle}>Meetings ({meetings.length})</h2>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {meetings.map(m => (
                  <MeetingRow key={m.id} meeting={m} onView={() => openMeeting(m.id)} />
                ))}
              </div>
            </section>
          )}

          {/* Meeting viewer (inline) */}
          {selectedMeeting && (
            <section style={styles.section}>
              <MeetingViewer
                meeting={selectedMeeting.meeting}
                messages={selectedMeeting.messages}
                agents={agents}
                onClose={() => setSelectedMeeting(null)}
              />
            </section>
          )}
        </div>

        {/* Right sidebar: selected agent detail + budget */}
        <div style={styles.sidebar}>
          {selected && (
            <div style={styles.card}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 12 }}>
                <span style={{ fontWeight: 700, color: '#f1f5f9' }}>{selected.name}</span>
                <button onClick={() => setSelected(null)} style={styles.closeBtn}>✕</button>
              </div>
              <div style={styles.detailRow}><span style={styles.detailLabel}>Role</span><span>{selected.role}</span></div>
              <div style={styles.detailRow}><span style={styles.detailLabel}>Status</span><span>{selected.status}</span></div>
              <div style={styles.detailRow}><span style={styles.detailLabel}>Provider</span><span>{selected.provider} / {selected.model}</span></div>
              {selected.currentTask && (
                <div style={{ marginTop: 12, fontSize: 12, color: '#cbd5e1', borderTop: '1px solid #334155', paddingTop: 12 }}>
                  {selected.currentTask}
                </div>
              )}
            </div>
          )}

          {actions.length > 0 && (
            <div style={styles.card}>
              <div style={{ fontWeight: 700, color: '#f1f5f9', marginBottom: 10, fontSize: 12, textTransform: 'uppercase', letterSpacing: 0.5 }}>
                Actions ({actions.length})
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                {actions.map(a => (
                  <ActionRow key={a.id} action={a} agents={agents} onRun={handleRunAction} running={runningActionId === a.id} />
                ))}
              </div>
            </div>
          )}

          {budget && <BudgetMeter budget={budget} />}
        </div>
      </div>
    </div>
  )
}

function ActionRow({ action, agents, onRun, running }: { action: ActionItem; agents: Agent[]; onRun: (id: string) => void; running: boolean }) {
  const assignee = agents.find(a => a.id === action.agentId)
  const statusColor: Record<string, string> = {
    open: '#60a5fa', in_progress: '#f59e0b', blocked: '#ef4444', done: '#22c55e', cancelled: '#475569',
  }
  const canRun = action.status === 'open' || action.status === 'blocked'
  return (
    <div style={{ fontSize: 12, color: '#cbd5e1', borderLeft: `2px solid ${statusColor[action.status] ?? '#475569'}`, paddingLeft: 8, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
      <div>
        <div style={{ color: '#f1f5f9', marginBottom: 2 }}>{action.title}</div>
        <div style={{ color: '#64748b', fontSize: 11 }}>
          {action.status}{assignee ? ` · ${assignee.name}` : ''}
        </div>
      </div>
      {canRun && (
        <button
          onClick={() => onRun(action.id)}
          disabled={running}
          title="Run action"
          style={{
            background: 'none', border: '1px solid #334155', borderRadius: 4,
            color: running ? '#334155' : '#60a5fa', cursor: running ? 'default' : 'pointer',
            fontSize: 11, padding: '2px 6px', flexShrink: 0, marginLeft: 8,
          }}
        >
          ▶
        </button>
      )}
    </div>
  )
}

function MeetingRow({ meeting, onView }: { meeting: MeetingRecord; onView: () => void }) {
  const typeLabel: Record<string, string> = {
    kickoff_sync: 'Kickoff Sync', status_check: 'Status Check',
    decision_request: 'Decision', escalation: 'Escalation', briefing: 'Briefing',
  }
  const statusColor = meeting.status === 'completed' ? '#22c55e' : meeting.status === 'failed' ? '#ef4444' : '#f59e0b'
  return (
    <div style={{ background: '#1e293b', border: '1px solid #334155', borderRadius: 6, padding: '10px 12px', fontSize: 12, color: '#94a3b8' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 4 }}>
        <span style={{ color: '#f1f5f9', fontWeight: 600 }}>{typeLabel[meeting.meetingType] ?? meeting.meetingType}</span>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ color: statusColor, fontSize: 11 }}>
            {meeting.status === 'completed' ? '✓' : meeting.status === 'failed' ? '✗' : '⟳'}
          </span>
          <button onClick={onView} style={{ background: 'none', border: '1px solid #334155', borderRadius: 4, color: '#64748b', cursor: 'pointer', fontSize: 11, padding: '2px 8px' }}>
            View
          </button>
        </div>
      </div>
      <div style={{ color: '#64748b', fontSize: 11, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
        {meeting.topic}
      </div>
      <div style={{ color: '#475569', fontSize: 11, marginTop: 3 }}>
        {meeting.messageCount} messages{meeting.summary ? ` · ${meeting.summary.slice(0, 60)}…` : ''}
      </div>
    </div>
  )
}

function StatusBadge({ status }: { status: string }) {
  const color = status === 'active' ? '#22c55e' : status === 'bootstrapping' ? '#60a5fa' : status.startsWith('failed') ? '#ef4444' : '#94a3b8'
  return <span style={{ fontSize: 11, color, fontWeight: 600, textTransform: 'uppercase' as const }}>{status}</span>
}

function Spinner({ inline }: { inline?: boolean }) {
  return (
    <div style={{ ...(inline ? { display: 'inline-block' } : { padding: '4rem', textAlign: 'center' as const }), color: '#64748b', fontSize: 13 }}>
      Loading…
    </div>
  )
}

function ErrorMsg({ msg }: { msg: string }) {
  return <div style={{ padding: '2rem', color: '#ef4444', fontFamily: 'sans-serif' }}>{msg}</div>
}

const styles = {
  root: { fontFamily: 'system-ui, sans-serif', minHeight: '100vh' },
  header: { display: 'flex', alignItems: 'flex-start', gap: 16, marginBottom: 24 },
  back: { background: 'none', border: 'none', color: '#64748b', cursor: 'pointer', fontSize: 13, padding: 0, paddingTop: 2 },
  kpisLink: { color: '#60a5fa', textDecoration: 'none', fontSize: 13, fontWeight: 500, whiteSpace: 'nowrap' as const },
  terminateBtn: {
    background: 'none', border: '1px solid #ef4444', borderRadius: 6,
    color: '#ef4444', cursor: 'pointer', fontSize: 12, fontWeight: 600,
    padding: '4px 10px', whiteSpace: 'nowrap' as const,
  },
  goalText: { fontSize: 16, fontWeight: 600, color: '#f1f5f9', marginBottom: 4 },
  body: { display: 'flex', gap: 24, alignItems: 'flex-start' },
  main: { flex: 1, minWidth: 0 },
  sidebar: { width: 280, flexShrink: 0, display: 'flex', flexDirection: 'column' as const, gap: 16 },
  section: { marginBottom: 28 },
  sectionTitle: { fontSize: 12, fontWeight: 600, color: '#64748b', textTransform: 'uppercase' as const, letterSpacing: 0.5, marginBottom: 12 },
  agentGrid: { display: 'flex', flexWrap: 'wrap' as const, gap: 12 },
  card: { background: '#1e293b', border: '1px solid #334155', borderRadius: 8, padding: '1rem', fontSize: 13, color: '#94a3b8' },
  detailRow: { display: 'flex', justifyContent: 'space-between', marginBottom: 6 },
  detailLabel: { color: '#64748b' },
  closeBtn: { background: 'none', border: 'none', color: '#64748b', cursor: 'pointer', fontSize: 14, padding: 0 },
  toggleBtn: { background: 'none', border: 'none', color: '#64748b', cursor: 'pointer', fontSize: 11, padding: '0 4px' },
  bootstrapping: {
    display: 'flex', alignItems: 'center', gap: 12,
    padding: '2rem', color: '#64748b', fontSize: 14,
    background: '#1e293b', borderRadius: 8, border: '1px solid #334155',
  } as const,
  pulse: {
    width: 10, height: 10, borderRadius: '50%', background: '#60a5fa',
    animation: 'pulse 1.5s infinite',
  },
}
