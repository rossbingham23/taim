// Typed API client — all backend calls go through here.

const API_BASE = ''  // proxied by vite dev server; empty = same origin in production

function getToken(): string | null {
  return localStorage.getItem('taim_token')
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getToken()
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string>),
  }
  if (token) headers['Authorization'] = `Bearer ${token}`

  const res = await fetch(API_BASE + path, { ...options, headers })

  if (res.status === 401) {
    localStorage.removeItem('taim_token')
    window.location.href = '/login'
    throw new Error('Unauthorized')
  }

  if (!res.ok) {
    const text = await res.text()
    throw new Error(`API ${res.status}: ${text}`)
  }

  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

// ── Auth ──────────────────────────────────────────────────────────────────────

export interface LoginResponse {
  token: string
  expiresAt: string
}

export async function login(email: string, password: string): Promise<LoginResponse> {
  const res = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!res.ok) throw new Error('Invalid credentials')
  return res.json()
}

// ── Tasks ─────────────────────────────────────────────────────────────────────

export interface TaskRecord {
  id: string
  tenantId: string
  goal: string
  status: string
  budgetId?: string
  createdAt: string
  updatedAt: string
}

export interface SubmitTaskResult {
  id: string
  budgetId?: string
  status: string
}

export function submitTask(goal: string, budgetUsd: number, provider?: string): Promise<SubmitTaskResult> {
  return request('/api/tasks', {
    method: 'POST',
    body: JSON.stringify({ goal, budgetUsd, provider }),
  })
}

export function listTasks(): Promise<TaskRecord[]> {
  return request('/api/tasks')
}

export function getTask(taskId: string): Promise<{ task: TaskRecord; graph: TeamGraphResponse }> {
  return request(`/api/tasks/${taskId}`)
}

// ── Agents ────────────────────────────────────────────────────────────────────

export interface AgentResponse {
  id: string
  tenantId: string
  taskId?: string
  parentAgentId?: string
  name: string
  role: string
  charter: string
  status: string
  provider?: string
  model?: string
  durableEntityKey?: string
  createdAt: string
  updatedAt: string
}

export interface TeamNodeResponse {
  agentId: string
  name: string
  role: string
  status: string
  depth: number
  kpiIds: string[]
}

export interface TeamEdgeResponse {
  parentAgentId: string
  childAgentId: string
}

export interface TeamGraphResponse {
  taskId: string
  nodes: TeamNodeResponse[]
  edges: TeamEdgeResponse[]
}

export function listAgents(): Promise<AgentResponse[]> {
  return request('/api/agents')
}

export function getAgent(agentId: string): Promise<{ agent: AgentResponse; kpis: KpiResponse[]; directReports: AgentResponse[] }> {
  return request(`/api/agents/${agentId}`)
}

// ── KPIs ──────────────────────────────────────────────────────────────────────

export interface KpiResponse {
  id: string
  tenantId: string
  agentId: string
  parentKpiId?: string
  name: string
  description?: string
  targetValue?: string
  unit?: string
  direction: string
  createdAt: string
  children?: KpiResponse[]
}

export function listRootKpis(taskId: string): Promise<KpiResponse[]> {
  return request(`/api/kpis?taskId=${taskId}`)
}

export function recordKpiValue(kpiId: string, value: string, source?: string): Promise<void> {
  return request(`/api/kpis/${kpiId}/values`, {
    method: 'POST',
    body: JSON.stringify({ value, source }),
  })
}

// ── Approvals ─────────────────────────────────────────────────────────────────

export interface ApprovalResponse {
  id: string
  tenantId: string
  agentId: string
  toolName: string
  toolArguments: Record<string, unknown>
  description: string
  status: string
  scope: string
  scopeKey?: string
  decidedAt?: string
  durableRequestId?: string
  createdAt: string
}

export function listPendingApprovals(): Promise<ApprovalResponse[]> {
  return request('/api/approvals')
}

export function listApprovalHistory(taskId: string): Promise<ApprovalResponse[]> {
  return request(`/api/approvals/history?taskId=${taskId}`)
}

export function decideApproval(approvalId: string, approved: boolean, scope: string, scopeKey?: string): Promise<void> {
  return request(`/api/approvals/${approvalId}/decide`, {
    method: 'POST',
    body: JSON.stringify({ approved, scope, scopeKey }),
  })
}

// ── Reports ───────────────────────────────────────────────────────────────────

export interface ReportResponse {
  id: string
  agentId: string
  agentName: string
  title: string
  content: string
  generatedAt: string
}

export function listReports(taskId: string): Promise<ReportResponse[]> {
  return request(`/api/reports?taskId=${taskId}`)
}

// ── Activity ──────────────────────────────────────────────────────────────────

export function listActivity(taskId?: string, limit = 200): Promise<import('./types').Notification[]> {
  const params = new URLSearchParams({ limit: String(limit) })
  if (taskId) params.set('taskId', taskId)
  return request(`/api/activity?${params}`)
}

// ── Actions ───────────────────────────────────────────────────────────────────

export function listActions(taskId: string): Promise<import('./types').ActionItem[]> {
  return request(`/api/actions?taskId=${taskId}`)
}

export function updateAction(actionId: string, patch: { status?: string; title?: string; description?: string; priority?: number; agentId?: string }): Promise<import('./types').ActionItem> {
  return request(`/api/actions/${actionId}`, {
    method: 'PATCH',
    body: JSON.stringify(patch),
  })
}

export function executeAction(actionId: string): Promise<void> {
  return request(`/api/actions/${actionId}/execute`, { method: 'POST' })
}

// ── Meetings ──────────────────────────────────────────────────────────────────

export function listMeetings(taskId: string): Promise<import('./types').MeetingRecord[]> {
  return request(`/api/meetings?taskId=${taskId}`)
}

export function getMeeting(meetingId: string): Promise<{ meeting: import('./types').MeetingRecord; messages: import('./types').MeetingMessage[] }> {
  return request(`/api/meetings/${meetingId}`)
}

// ── Task Termination ──────────────────────────────────────────────────────────

export function terminateTask(taskId: string): Promise<void> {
  return request(`/api/tasks/${taskId}/terminate`, { method: 'POST' })
}

// ── System Stop ───────────────────────────────────────────────────────────────

export function getSystemStatus(): Promise<{ stopped: boolean }> {
  return request('/api/system/status')
}

export function stopSystem(): Promise<void> {
  return request('/api/system/stop', { method: 'POST' })
}

export function resumeSystem(): Promise<void> {
  return request('/api/system/resume', { method: 'POST' })
}
