// ── Agent ─────────────────────────────────────────────────────────────────────
export type AgentStatus = 'idle' | 'active' | 'waiting_approval' | 'sleeping' | 'terminated'
export type AgentRole =
  | 'bootstrap' | 'expert'
  | 'ceo' | 'cto' | 'cmo' | 'cfo' | 'hr'
  | 'developer' | 'designer' | 'qaEngineer' | 'qaManager' | 'productManager'
  | 'marketingSpecialist' | 'contentWriter' | 'dataAnalyst'
  | 'salesRepresentative' | 'customerSupport' | 'generic'

export interface Agent {
  id: string
  name: string
  role: AgentRole
  status: AgentStatus
  currentTask?: string
  parentId?: string
  provider: string
  model: string
  createdAt: string
}


// ── Team ──────────────────────────────────────────────────────────────────────
export interface TeamNode {
  agentId: string
  name: string
  role: AgentRole
  status: AgentStatus
}

export interface TeamEdge {
  fromAgentId: string
  toAgentId: string
  label?: string
}

export interface TeamGraph {
  taskId: string
  nodes: TeamNode[]
  edges: TeamEdge[]
}

// ── KPI ───────────────────────────────────────────────────────────────────────
export type KpiDirection = 'higher_better' | 'lower_better' | 'target'

export interface KpiNode {
  id: string
  agentId: string
  agentName: string
  name: string
  description: string
  unit: string
  direction: KpiDirection
  targetValue?: number
  currentValue?: number
  parentId?: string
  children: KpiNode[]
  updatedAt: string
}

// ── Approval ──────────────────────────────────────────────────────────────────
export type ApprovalScope = 'once' | 'agent_and_tool' | 'agent_tool_and_param'
export type ApprovalStatus = 'pending' | 'approved' | 'denied' | 'expired'

export interface ApprovalRequest {
  id: string
  agentId: string
  agentName: string
  toolName: string
  toolDescription: string
  parameters: Record<string, unknown>
  status: ApprovalStatus
  requestedAt: string
}

// ── Meeting ───────────────────────────────────────────────────────────────────
export type MeetingKind = 'kickoff_sync' | 'status_check' | 'decision_request' | 'escalation' | 'briefing'
export type MeetingStatus = 'in_progress' | 'completed' | 'failed'

export interface MeetingRecord {
  id: string
  tenantId: string
  taskId?: string
  topic: string
  meetingType: MeetingKind
  status: MeetingStatus
  organizerAgentId?: string
  participantAgentIds: string[]
  summary?: string
  messageCount: number
  startedAt: string
  completedAt?: string
}

export interface MeetingMessage {
  id: string
  meetingId: string
  speakerAgentId?: string
  content: string
  sequence: number
  createdAt: string
}

// ── Budget ────────────────────────────────────────────────────────────────────
export type BudgetStatus = 'active' | 'paused' | 'exhausted'

export interface BudgetInfo {
  id: string
  limitUsd: number
  spentUsd: number
  status: BudgetStatus
  byAgent: Array<{ agentId: string; agentName: string; totalCostUsd: number; totalTokens: number }>
}

// ── Executive Report ──────────────────────────────────────────────────────────
export interface ExecutiveReport {
  id: string
  agentId: string
  agentName: string
  title: string
  content: string
  generatedAt: string
}

// ── Notifications (SignalR) ───────────────────────────────────────────────────
// ── Action ────────────────────────────────────────────────────────────────────
export type ActionStatus = 'open' | 'in_progress' | 'blocked' | 'done' | 'cancelled'

export interface ActionItem {
  id: string
  tenantId: string
  taskId: string
  agentId?: string
  createdByAgentId?: string
  title: string
  description?: string
  status: ActionStatus
  priority: number
  parentActionId?: string
  dueAt?: string
  completedAt?: string
  createdAt: string
  updatedAt: string
}

export type NotificationKind =
  | 'approval_required'
  | 'agent_status_changed'
  | 'executive_report'
  | 'budget_alert'
  | 'team_update'
  | 'meeting_started'
  | 'meeting_message'
  | 'meeting_completed'
  | 'agent_log'
  | 'action_created'
  | 'action_updated'
  | 'task_terminated'
  | 'system_stopped'
  | 'system_resumed'

export interface Notification {
  id: string
  kind: NotificationKind
  tenantId: string
  title: string
  body: string
  metadata: Record<string, unknown>
  createdAt: string
}
