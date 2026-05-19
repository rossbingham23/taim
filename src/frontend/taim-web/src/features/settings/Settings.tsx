import { useState, useEffect } from 'react'
import { getSystemStatus, stopSystem, resumeSystem } from '../../lib/api'
import { onNotification } from '../../lib/signalr'

interface ProviderField {
  key: string
  label: string
  placeholder: string
  type?: 'text' | 'password'
}

const PROVIDERS: Array<{ name: string; id: string; fields: ProviderField[] }> = [
  {
    name: 'Anthropic',
    id: 'anthropic',
    fields: [
      { key: 'ANTHROPIC_API_KEY', label: 'API Key', placeholder: 'sk-ant-…', type: 'password' },
    ],
  },
  {
    name: 'OpenAI',
    id: 'openai',
    fields: [
      { key: 'OPENAI_API_KEY', label: 'API Key', placeholder: 'sk-…', type: 'password' },
    ],
  },
  {
    name: 'Gemini',
    id: 'gemini',
    fields: [
      { key: 'GEMINI_API_KEY', label: 'API Key', placeholder: 'AIza…', type: 'password' },
    ],
  },
  {
    name: 'Ollama',
    id: 'ollama',
    fields: [
      { key: 'OLLAMA_BASE_URL', label: 'Base URL', placeholder: 'http://localhost:11434' },
      { key: 'OLLAMA_MODEL', label: 'Model', placeholder: 'qwen2.5:3b' },
    ],
  },
  {
    name: 'Web Search (Brave)',
    id: 'brave',
    fields: [
      { key: 'BRAVE_API_KEY', label: 'API Key', placeholder: 'BSA…', type: 'password' },
    ],
  },
  {
    name: 'Email (SMTP)',
    id: 'smtp',
    fields: [
      { key: 'SMTP_HOST', label: 'SMTP Host', placeholder: 'smtp.gmail.com' },
      { key: 'SMTP_PORT', label: 'SMTP Port', placeholder: '587' },
      { key: 'SMTP_USER', label: 'Username', placeholder: 'you@example.com' },
      { key: 'SMTP_PASS', label: 'Password', placeholder: '…', type: 'password' },
      { key: 'FROM_ADDRESS', label: 'From Address', placeholder: 'taim@example.com' },
    ],
  },
]

export function Settings() {
  const [isStopped, setIsStopped] = useState<boolean | null>(null)
  const [toggling, setToggling] = useState(false)

  useEffect(() => {
    getSystemStatus().then(s => setIsStopped(s.stopped)).catch(() => {})
  }, [])

  useEffect(() => {
    return onNotification(n => {
      if (n.kind === 'system_stopped') setIsStopped(true)
      if (n.kind === 'system_resumed') setIsStopped(false)
    })
  }, [])

  const handleToggle = async () => {
    setToggling(true)
    try {
      if (isStopped) {
        await resumeSystem()
        setIsStopped(false)
      } else {
        await stopSystem()
        setIsStopped(true)
      }
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Request failed')
    } finally {
      setToggling(false)
    }
  }

  return (
    <div style={styles.root}>
      <h1 style={styles.heading}>Settings</h1>

      <div style={{ ...styles.section, marginBottom: 28 }}>
        <h2 style={styles.sectionTitle}>System Controls</h2>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
          <div>
            <div style={{ fontSize: 13, color: '#94a3b8', marginBottom: 4 }}>Agent Activity</div>
            <div style={{ fontSize: 13, fontWeight: 600 }}>
              {isStopped === null ? (
                <span style={{ color: '#64748b' }}>Loading…</span>
              ) : isStopped ? (
                <span style={{ color: '#ef4444' }}>● Stopped</span>
              ) : (
                <span style={{ color: '#22c55e' }}>● Running</span>
              )}
            </div>
          </div>
          <button
            onClick={handleToggle}
            disabled={toggling || isStopped === null}
            style={{
              background: 'none',
              border: `1px solid ${isStopped ? '#22c55e' : '#ef4444'}`,
              borderRadius: 6, cursor: toggling ? 'default' : 'pointer',
              color: isStopped ? '#22c55e' : '#ef4444',
              fontSize: 13, fontWeight: 600, padding: '6px 14px',
            }}
          >
            {toggling ? '…' : isStopped ? 'Resume All Agents' : 'Stop All Agents'}
          </button>
        </div>
        <div style={styles.envNote}>
          <span style={styles.envNoteIcon}>ℹ</span>
          Stopping halts all work loops immediately but preserves all task and action state. Resume to continue from where you left off.
        </div>
      </div>
      <p style={styles.intro}>
        Provider credentials are configured via environment variables in your <code style={styles.code}>.env</code> file or Docker Compose.
        Reference this page when setting up your environment.
      </p>

      <div>
        {PROVIDERS.map(provider => (
          <div key={provider.id} style={styles.section}>
            <h2 style={styles.sectionTitle}>{provider.name}</h2>
            <div style={styles.fields}>
              {provider.fields.map(field => (
                <div key={field.key} style={styles.field}>
                  <label style={styles.label}>{field.label}</label>
                  <div style={styles.envRow}>
                    <code style={styles.envVar}>{field.key}</code>
                    <input
                      type={field.type ?? 'text'}
                      placeholder={field.placeholder}
                      style={styles.input}
                      readOnly
                      tabIndex={-1}
                    />
                  </div>
                </div>
              ))}
            </div>
          </div>
        ))}

        <div style={styles.envNote}>
          <span style={styles.envNoteIcon}>ℹ</span>
          Edit your <code style={styles.code}>.env</code> file and restart services:{' '}
          <code style={styles.code}>docker compose up --build</code>
        </div>
      </div>
    </div>
  )
}

const styles = {
  root: { maxWidth: 680, fontFamily: 'system-ui, sans-serif' },
  heading: { fontSize: 20, fontWeight: 700, color: '#f1f5f9', marginBottom: 8 },
  intro: { fontSize: 14, color: '#94a3b8', marginBottom: 28, lineHeight: 1.6 },
  section: { marginBottom: 28, background: '#1e293b', borderRadius: 8, padding: '1.25rem', border: '1px solid #334155' },
  sectionTitle: { fontSize: 14, fontWeight: 700, color: '#f1f5f9', marginBottom: 16 },
  fields: { display: 'flex', flexDirection: 'column' as const, gap: 12 },
  field: {},
  label: { fontSize: 12, color: '#64748b', display: 'block', marginBottom: 4 },
  envRow: { display: 'flex', gap: 8, alignItems: 'center' },
  envVar: { fontSize: 12, color: '#60a5fa', background: '#0f172a', padding: '4px 8px', borderRadius: 4, whiteSpace: 'nowrap' as const },
  input: {
    flex: 1, background: '#0f172a', border: '1px solid #334155', borderRadius: 6,
    padding: '8px 10px', color: '#475569', fontSize: 13, outline: 'none',
  },
  code: { fontFamily: 'monospace', fontSize: 13, color: '#60a5fa', background: '#0f172a', padding: '1px 5px', borderRadius: 3 },
  envNote: {
    display: 'flex', alignItems: 'center', gap: 8, padding: '12px 16px',
    background: '#0f172a', border: '1px solid #334155', borderRadius: 8,
    fontSize: 13, color: '#94a3b8', marginTop: 8,
  },
  envNoteIcon: { color: '#60a5fa', fontSize: 16, flexShrink: 0 },
}
