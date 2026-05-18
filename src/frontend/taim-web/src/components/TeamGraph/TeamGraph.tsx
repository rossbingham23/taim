import type { TeamGraph as TeamGraphType, TeamNode } from '../../lib/types'

interface TeamGraphProps {
  graph: TeamGraphType
  onNodeClick?: (node: TeamNode) => void
}

const STATUS_COLORS: Record<string, string> = {
  idle: '#94a3b8',
  active: '#22c55e',
  waiting_approval: '#f59e0b',
  sleeping: '#6366f1',
  terminated: '#ef4444',
}

const ROLE_ABBREV: Record<string, string> = {
  ceo: 'CEO', cto: 'CTO', cmo: 'CMO', cfo: 'CFO', hr: 'HR',
  developer: 'DEV', designer: 'DSN', qaEngineer: 'QA', qaManager: 'QAM',
  productManager: 'PM', marketingSpecialist: 'MKT',
  contentWriter: 'CW', dataAnalyst: 'DA',
  salesRepresentative: 'SLS', customerSupport: 'CS',
  bootstrap: 'BST', expert: 'EXP', generic: 'GEN',
}

// Lay out nodes in a simple top-down hierarchy tree.
function computeLayout(graph: TeamGraphType): Map<string, { x: number; y: number }> {
  const positions = new Map<string, { x: number; y: number }>()
  const children = new Map<string, string[]>()
  const hasParent = new Set<string>()

  for (const edge of graph.edges) {
    if (!children.has(edge.fromAgentId)) children.set(edge.fromAgentId, [])
    children.get(edge.fromAgentId)!.push(edge.toAgentId)
    hasParent.add(edge.toAgentId)
  }

  const roots = graph.nodes.filter(n => !hasParent.has(n.agentId)).map(n => n.agentId)

  const NODE_W = 100
  const NODE_H = 50
  const H_GAP = 20
  const V_GAP = 60

  function subtreeWidth(id: string): number {
    const kids = children.get(id) ?? []
    if (kids.length === 0) return NODE_W
    const childrenWidth = kids.reduce((sum, k) => sum + subtreeWidth(k) + H_GAP, -H_GAP)
    return Math.max(NODE_W, childrenWidth)
  }

  function place(id: string, x: number, y: number) {
    positions.set(id, { x, y })
    const kids = children.get(id) ?? []
    if (kids.length === 0) return
    const totalWidth = kids.reduce((sum, k) => sum + subtreeWidth(k) + H_GAP, -H_GAP)
    let curX = x - totalWidth / 2
    for (const kid of kids) {
      const w = subtreeWidth(kid)
      place(kid, curX + w / 2, y + NODE_H + V_GAP)
      curX += w + H_GAP
    }
  }

  let rx = NODE_W / 2
  for (const root of roots) {
    const w = subtreeWidth(root)
    place(root, rx + w / 2, NODE_H / 2 + 10)
    rx += w + H_GAP * 2
  }

  return positions
}

export function TeamGraph({ graph, onNodeClick }: TeamGraphProps) {
  if (graph.nodes.length === 0) {
    return (
      <div style={{ padding: '2rem', color: '#475569', fontFamily: 'sans-serif', textAlign: 'center' }}>
        Team is being assembled…
      </div>
    )
  }

  const NODE_W = 100
  const NODE_H = 46

  const positions = computeLayout(graph)
  const nodeMap = new Map(graph.nodes.map(n => [n.agentId, n]))

  const allX = [...positions.values()].map(p => p.x)
  const allY = [...positions.values()].map(p => p.y)
  const minX = Math.min(...allX) - NODE_W / 2 - 16
  const minY = Math.min(...allY) - NODE_H / 2 - 16
  const maxX = Math.max(...allX) + NODE_W / 2 + 16
  const maxY = Math.max(...allY) + NODE_H / 2 + 16
  const vw = maxX - minX
  const vh = maxY - minY

  return (
    <div style={{ background: '#0f172a', borderRadius: 8, border: '1px solid #1e293b', overflow: 'auto', padding: 8 }}>
      <svg
        viewBox={`${minX} ${minY} ${vw} ${vh}`}
        width="100%"
        style={{ minHeight: 120, display: 'block' }}
      >
        {/* Edges */}
        {graph.edges.map(edge => {
          const from = positions.get(edge.fromAgentId)
          const to = positions.get(edge.toAgentId)
          if (!from || !to) return null
          return (
            <line
              key={`${edge.fromAgentId}-${edge.toAgentId}`}
              x1={from.x} y1={from.y + NODE_H / 2}
              x2={to.x} y2={to.y - NODE_H / 2}
              stroke="#334155" strokeWidth={1.5}
            />
          )
        })}

        {/* Nodes */}
        {graph.nodes.map(node => {
          const pos = positions.get(node.agentId)
          if (!pos) return null
          const color = STATUS_COLORS[node.status] ?? '#94a3b8'
          return (
            <g
              key={node.agentId}
              transform={`translate(${pos.x - NODE_W / 2}, ${pos.y - NODE_H / 2})`}
              onClick={() => onNodeClick?.(nodeMap.get(node.agentId)!)}
              style={{ cursor: onNodeClick ? 'pointer' : 'default' }}
            >
              <rect width={NODE_W} height={NODE_H} rx={6} fill="#1e293b" stroke={color} strokeWidth={1.5} />
              <text x={NODE_W / 2} y={16} textAnchor="middle" fill="#f1f5f9" fontSize={11} fontWeight="bold" fontFamily="sans-serif">
                {ROLE_ABBREV[node.role] ?? node.role.toUpperCase()}
              </text>
              <text x={NODE_W / 2} y={29} textAnchor="middle" fill="#94a3b8" fontSize={9} fontFamily="sans-serif">
                {node.name.length > 14 ? node.name.slice(0, 13) + '…' : node.name}
              </text>
              <circle cx={NODE_W - 8} cy={8} r={4} fill={color} />
            </g>
          )
        })}
      </svg>
    </div>
  )
}
