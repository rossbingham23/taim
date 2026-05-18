import type { BudgetInfo } from '../../lib/types'

interface BudgetMeterProps {
  budget: BudgetInfo
}

export function BudgetMeter({ budget }: BudgetMeterProps) {
  const pct = budget.limitUsd > 0 ? Math.min(100, (budget.spentUsd / budget.limitUsd) * 100) : 0
  const remaining = Math.max(0, budget.limitUsd - budget.spentUsd)

  const barColor =
    budget.status === 'exhausted' ? '#ef4444' :
    pct >= 80 ? '#f59e0b' :
    '#22c55e'

  return (
    <div style={{ fontFamily: 'sans-serif', background: '#0f172a', borderRadius: 8, padding: '1rem', border: '1px solid #1e293b' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 8 }}>
        <span style={{ fontWeight: 700, color: '#f1f5f9', fontSize: 14 }}>Budget</span>
        <span style={{ fontSize: 12, color: budget.status === 'exhausted' ? '#ef4444' : budget.status === 'paused' ? '#f59e0b' : '#22c55e' }}>
          {budget.status.charAt(0).toUpperCase() + budget.status.slice(1)}
        </span>
      </div>
      <div style={{ background: '#1e293b', borderRadius: 4, height: 8, marginBottom: 8, overflow: 'hidden' }}>
        <div style={{ width: `${pct}%`, height: '100%', background: barColor, borderRadius: 4, transition: 'width 0.3s ease' }} />
      </div>
      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: '#94a3b8', marginBottom: 12 }}>
        <span>${budget.spentUsd.toFixed(4)} spent</span>
        <span>${remaining.toFixed(4)} remaining of ${budget.limitUsd.toFixed(2)}</span>
      </div>
      {budget.byAgent.length > 0 && (
        <div style={{ borderTop: '1px solid #1e293b', paddingTop: 8 }}>
          <div style={{ fontSize: 11, color: '#64748b', marginBottom: 6, textTransform: 'uppercase' }}>By Agent</div>
          {budget.byAgent.slice(0, 5).map(a => (
            <div key={a.agentId} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: '#cbd5e1', marginBottom: 4 }}>
              <span>{a.agentName}</span>
              <span>${a.totalCostUsd.toFixed(4)} ({a.totalTokens.toLocaleString()} tokens)</span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
