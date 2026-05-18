import type { Meta, StoryObj } from '@storybook/react'
import { TeamGraph } from './TeamGraph'
import type { TeamGraph as TeamGraphType } from '../../lib/types'

const meta = {
  title: 'Components/TeamGraph',
  component: TeamGraph,
  parameters: { layout: 'padded', backgrounds: { default: 'dark' } },
  tags: ['autodocs'],
} satisfies Meta<typeof TeamGraph>

export default meta
type Story = StoryObj<typeof meta>

const graph: TeamGraphType = {
  taskId: 'task-001',
  nodes: [
    { agentId: 'ceo', name: 'Alex (CEO)', role: 'ceo', status: 'active' },
    { agentId: 'cto', name: 'Sam (CTO)', role: 'cto', status: 'active' },
    { agentId: 'cmo', name: 'Maya (CMO)', role: 'cmo', status: 'idle' },
    { agentId: 'cfo', name: 'Jordan (CFO)', role: 'cfo', status: 'sleeping' },
    { agentId: 'dev1', name: 'Dev Agent 1', role: 'developer', status: 'active' },
    { agentId: 'dev2', name: 'Dev Agent 2', role: 'developer', status: 'waiting_approval' },
    { agentId: 'qa', name: 'QA Bot', role: 'qaEngineer', status: 'idle' },
  ],
  edges: [
    { fromAgentId: 'ceo', toAgentId: 'cto' },
    { fromAgentId: 'ceo', toAgentId: 'cmo' },
    { fromAgentId: 'ceo', toAgentId: 'cfo' },
    { fromAgentId: 'cto', toAgentId: 'dev1' },
    { fromAgentId: 'cto', toAgentId: 'dev2' },
    { fromAgentId: 'cto', toAgentId: 'qa' },
  ],
}

export const FullTeam: Story = {
  args: { graph, onNodeClick: (node) => console.log('Clicked:', node) },
}

export const Empty: Story = {
  args: { graph: { taskId: 'task-001', nodes: [], edges: [] } },
}
