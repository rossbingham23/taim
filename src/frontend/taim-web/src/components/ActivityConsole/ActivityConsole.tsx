import { useRef, useEffect, useState } from 'react'
import type { Notification, NotificationKind } from '../../lib/types'

interface ActivityConsoleProps {
  entries: Notification[]
  title?: string
  maxHeight?: number
}

const KIND_COLORS: Record<NotificationKind, string> = {
  agent_log:             '#64748b',
  agent_status_changed:  '#60a5fa',
  executive_report:      '#22c55e',
  team_update:           '#a78bfa',
  approval_required:     '#f59e0b',
  budget_alert:          '#ef4444',
  meeting_started:       '#34d399',
  meeting_message:       '#2dd4bf',
  meeting_completed:     '#10b981',
  action_created:        '#818cf8',
  action_updated:        '#6366f1',
}

const KIND_LABELS: Record<NotificationKind, string> = {
  agent_log:             'LOG',
  agent_status_changed:  'STATUS',
  executive_report:      'REPORT',
  team_update:           'TEAM',
  approval_required:     'APPROVAL',
  budget_alert:          'BUDGET',
  meeting_started:       'MEETING',
  meeting_message:       'MEETING',
  meeting_completed:     'MEETING',
  action_created:        'ACTION',
  action_updated:        'ACTION',
}

function formatTime(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false })
  } catch {
    return '??:??:??'
  }
}

export function ActivityConsole({ entries, title = 'Activity', maxHeight = 320 }: ActivityConsoleProps) {
  const bottomRef = useRef<HTMLDivElement>(null)
  const [expanded, setExpanded] = useState<string | null>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [entries.length])

  return (
    <div style={styles.wrapper}>
      <div style={styles.header}>
        <span style={styles.headerTitle}>{title}</span>
        <span style={styles.count}>{entries.length} events</span>
      </div>
      <div style={{ ...styles.console, maxHeight }}>
        {entries.length === 0 ? (
          <div style={styles.empty}>No activity yet…</div>
        ) : (
          entries.map(entry => {
            const color = KIND_COLORS[entry.kind] ?? '#64748b'
            const label = KIND_LABELS[entry.kind] ?? entry.kind.toUpperCase()
            const isExpanded = expanded === entry.id
            const hasMetadata = entry.metadata && Object.keys(entry.metadata).length > 0
            return (
              <div key={entry.id} style={styles.row}>
                <span style={styles.time}>{formatTime(entry.createdAt)}</span>
                <span style={{ ...styles.badge, background: color + '22', color }}>{label}</span>
                <span style={styles.message}>{entry.title}</span>
                {entry.body && <span style={styles.body}> — {entry.body}</span>}
                {hasMetadata && (
                  <button
                    style={styles.expandBtn}
                    onClick={() => setExpanded(isExpanded ? null : entry.id)}
                  >
                    {isExpanded ? '▲' : '▼'}
                  </button>
                )}
                {isExpanded && hasMetadata && (
                  <div style={styles.meta}>
                    {JSON.stringify(entry.metadata, null, 2)}
                  </div>
                )}
              </div>
            )
          })
        )}
        <div ref={bottomRef} />
      </div>
    </div>
  )
}

const styles = {
  wrapper: {
    background: '#020617',
    border: '1px solid #1e293b',
    borderRadius: 6,
    overflow: 'hidden',
    fontFamily: '"Menlo", "Consolas", "Monaco", monospace',
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: '6px 12px',
    background: '#0f172a',
    borderBottom: '1px solid #1e293b',
  },
  headerTitle: { fontSize: 11, fontWeight: 600, color: '#64748b', textTransform: 'uppercase' as const, letterSpacing: 1 },
  count: { fontSize: 11, color: '#334155' },
  console: {
    overflowY: 'auto' as const,
    padding: '8px 0',
  },
  empty: { padding: '12px 12px', color: '#334155', fontSize: 12 },
  row: {
    display: 'flex',
    flexWrap: 'wrap' as const,
    alignItems: 'baseline',
    gap: 6,
    padding: '2px 12px',
    fontSize: 12,
    lineHeight: 1.6,
    borderBottom: '1px solid #0f172a',
  },
  time: { color: '#334155', flexShrink: 0 },
  badge: {
    fontSize: 10,
    fontWeight: 700,
    padding: '1px 5px',
    borderRadius: 3,
    flexShrink: 0,
    letterSpacing: 0.5,
  },
  message: { color: '#cbd5e1', wordBreak: 'break-word' as const },
  body: { color: '#64748b', fontSize: 11 },
  expandBtn: {
    background: 'none',
    border: 'none',
    color: '#475569',
    cursor: 'pointer',
    fontSize: 10,
    padding: '0 2px',
    marginLeft: 'auto',
  },
  meta: {
    width: '100%',
    marginTop: 4,
    padding: '6px 8px',
    background: '#0f172a',
    borderRadius: 4,
    fontSize: 11,
    color: '#475569',
    whiteSpace: 'pre' as const,
    overflowX: 'auto' as const,
  },
}
