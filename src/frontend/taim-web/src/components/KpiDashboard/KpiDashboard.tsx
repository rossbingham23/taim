import type { KpiNode } from '../../lib/types'

interface KpiDashboardProps {
  roots: KpiNode[]
}

function KpiNodeRow({ node, depth = 0 }: { node: KpiNode; depth?: number }) {
  const hasValue = node.currentValue !== undefined
  const hasTarget = node.targetValue !== undefined
  const pct = hasValue && hasTarget && node.targetValue !== 0
    ? Math.min(100, Math.round((node.currentValue! / node.targetValue!) * 100))
    : null

  const statusColor =
    pct === null ? '#94a3b8' :
    pct >= 90 ? '#22c55e' :
    pct >= 60 ? '#f59e0b' :
    '#ef4444'

  return (
    <>
      <div
        style={{
          paddingLeft: depth * 20 + 12,
          paddingRight: 12,
          paddingTop: 8,
          paddingBottom: 8,
          borderBottom: '1px solid #1e293b',
          display: 'grid',
          gridTemplateColumns: '1fr auto auto',
          gap: 12,
          alignItems: 'center',
          background: depth % 2 === 0 ? '#0f172a' : '#111827',
        }}
      >
        <div>
          <div style={{ fontWeight: depth === 0 ? 700 : 500, fontSize: 13, color: '#f1f5f9' }}>{node.name}</div>
          <div style={{ fontSize: 11, color: '#64748b' }}>{node.agentName}</div>
        </div>
        <div style={{ textAlign: 'right', fontSize: 12 }}>
          {hasValue ? (
            <span style={{ color: '#cbd5e1' }}>
              {node.currentValue} {node.unit}
            </span>
          ) : (
            <span style={{ color: '#475569' }}>—</span>
          )}
        </div>
        <div style={{ width: 60, textAlign: 'right', fontSize: 12, fontWeight: 700, color: statusColor }}>
          {pct !== null ? `${pct}%` : '—'}
        </div>
      </div>
      {node.children.map(child => (
        <KpiNodeRow key={child.id} node={child} depth={depth + 1} />
      ))}
    </>
  )
}

export function KpiDashboard({ roots }: KpiDashboardProps) {
  if (roots.length === 0) {
    return (
      <div style={{ padding: '2rem', color: '#475569', fontFamily: 'sans-serif', textAlign: 'center' }}>
        No KPIs defined yet. Agents will create KPIs as they start working.
      </div>
    )
  }

  return (
    <div style={{ fontFamily: 'sans-serif', background: '#0f172a', borderRadius: 8, overflow: 'hidden', border: '1px solid #1e293b' }}>
      <div style={{ padding: '12px 16px', background: '#1e293b', display: 'grid', gridTemplateColumns: '1fr auto auto', gap: 12 }}>
        <div style={{ fontSize: 12, fontWeight: 600, color: '#94a3b8', textTransform: 'uppercase' }}>KPI</div>
        <div style={{ fontSize: 12, fontWeight: 600, color: '#94a3b8', textTransform: 'uppercase' }}>Current</div>
        <div style={{ fontSize: 12, fontWeight: 600, color: '#94a3b8', textTransform: 'uppercase', width: 60, textAlign: 'right' }}>Progress</div>
      </div>
      {roots.map(root => (
        <KpiNodeRow key={root.id} node={root} />
      ))}
    </div>
  )
}
