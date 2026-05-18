import { useState, useEffect, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { submitTask, listTasks, type TaskRecord } from '../../lib/api'

const PROVIDERS = ['', 'anthropic', 'openai', 'gemini', 'ollama']

export function TaskIntake() {
  const navigate = useNavigate()
  const [goal, setGoal] = useState('')
  const [budget, setBudget] = useState('100')
  const [provider, setProvider] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [recent, setRecent] = useState<TaskRecord[]>([])

  useEffect(() => {
    listTasks().then(setRecent).catch(() => {})
  }, [])

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!goal.trim()) return
    setError(null)
    setSubmitting(true)
    try {
      const result = await submitTask(goal.trim(), parseFloat(budget), provider || undefined)
      navigate(`/tasks/${result.id}`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to submit task')
      setSubmitting(false)
    }
  }

  return (
    <div style={styles.root}>
      <div style={styles.col}>
        <h1 style={styles.heading}>Submit a Goal</h1>
        <p style={styles.sub}>
          Describe what you want to accomplish. TAIM will assemble a team of AI agents to make it happen.
        </p>

        <form onSubmit={handleSubmit} style={styles.form}>
          <label style={styles.label}>Your Goal</label>
          <textarea
            value={goal}
            onChange={e => setGoal(e.target.value)}
            placeholder="e.g. Start a consulting company focused on AI adoption for SMBs, budget $100"
            rows={4}
            style={styles.textarea}
            required
          />

          <div style={styles.row}>
            <div style={{ flex: 1 }}>
              <label style={styles.label}>Budget (USD)</label>
              <input
                type="number"
                value={budget}
                onChange={e => setBudget(e.target.value)}
                min="1"
                step="any"
                style={styles.input}
                required
              />
            </div>
            <div style={{ flex: 1 }}>
              <label style={styles.label}>LLM Provider (optional)</label>
              <select value={provider} onChange={e => setProvider(e.target.value)} style={styles.select}>
                {PROVIDERS.map(p => (
                  <option key={p} value={p}>{p || 'Auto (tenant default)'}</option>
                ))}
              </select>
            </div>
          </div>

          {error && <div style={styles.error}>{error}</div>}

          <button type="submit" disabled={submitting || !goal.trim()} style={{
            ...styles.button,
            opacity: (submitting || !goal.trim()) ? 0.6 : 1,
          }}>
            {submitting ? 'Launching team…' : 'Launch AI Team →'}
          </button>
        </form>
      </div>

      {recent.length > 0 && (
        <div style={styles.recentCol}>
          <h2 style={styles.recentHeading}>Recent Goals</h2>
          {recent.map(task => (
            <button
              key={task.id}
              onClick={() => navigate(`/tasks/${task.id}`)}
              style={styles.taskCard}
            >
              <div style={styles.taskGoal}>{task.goal}</div>
              <div style={styles.taskMeta}>
                <StatusBadge status={task.status} />
                <span style={styles.taskDate}>{new Date(task.createdAt).toLocaleDateString()}</span>
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

function StatusBadge({ status }: { status: string }) {
  const colors: Record<string, string> = {
    active: '#22c55e',
    bootstrapping: '#60a5fa',
    completed: '#94a3b8',
  }
  const color = colors[status] ?? (status.startsWith('failed') ? '#ef4444' : '#f59e0b')
  return (
    <span style={{ fontSize: 11, color, fontWeight: 600, textTransform: 'uppercase' as const }}>
      {status}
    </span>
  )
}

const styles = {
  root: {
    display: 'flex',
    gap: 32,
    maxWidth: 960,
    margin: '0 auto',
    fontFamily: 'system-ui, sans-serif',
    alignItems: 'flex-start',
  } as const,
  col: { flex: 2 },
  heading: { fontSize: 24, fontWeight: 700, color: '#f1f5f9', marginBottom: 8 },
  sub: { fontSize: 14, color: '#94a3b8', marginBottom: 24, lineHeight: 1.6 },
  form: { display: 'flex', flexDirection: 'column' as const, gap: 16 },
  label: { display: 'block', fontSize: 12, color: '#94a3b8', marginBottom: 6, textTransform: 'uppercase' as const, letterSpacing: 0.5 },
  textarea: {
    background: '#0f172a', border: '1px solid #334155', borderRadius: 8,
    padding: 12, color: '#f1f5f9', fontSize: 15, resize: 'vertical' as const,
    width: '100%', boxSizing: 'border-box' as const, outline: 'none', lineHeight: 1.6,
  },
  input: {
    background: '#0f172a', border: '1px solid #334155', borderRadius: 6,
    padding: '10px 12px', color: '#f1f5f9', fontSize: 14, width: '100%',
    boxSizing: 'border-box' as const, outline: 'none',
  },
  select: {
    background: '#0f172a', border: '1px solid #334155', borderRadius: 6,
    padding: '10px 12px', color: '#f1f5f9', fontSize: 14, width: '100%',
    boxSizing: 'border-box' as const, outline: 'none',
  },
  row: { display: 'flex', gap: 12 },
  error: { color: '#ef4444', fontSize: 13 },
  button: {
    background: '#3b82f6', color: '#fff', border: 'none', borderRadius: 8,
    padding: '12px 0', fontSize: 15, fontWeight: 600, cursor: 'pointer',
  } as const,
  recentCol: { flex: 1, minWidth: 240 },
  recentHeading: { fontSize: 14, fontWeight: 600, color: '#64748b', marginBottom: 12, textTransform: 'uppercase' as const, letterSpacing: 0.5 },
  taskCard: {
    width: '100%', background: '#1e293b', border: '1px solid #334155',
    borderRadius: 8, padding: '12px 14px', marginBottom: 8, cursor: 'pointer',
    textAlign: 'left' as const, color: 'inherit',
  },
  taskGoal: { fontSize: 13, color: '#f1f5f9', marginBottom: 8, lineHeight: 1.4 },
  taskMeta: { display: 'flex', justifyContent: 'space-between', alignItems: 'center' },
  taskDate: { fontSize: 11, color: '#64748b' },
}
