import type { Meta, StoryObj } from '@storybook/react'
import { AgentCard } from './AgentCard'

const meta = {
  title: 'Components/AgentCard',
  component: AgentCard,
  parameters: { layout: 'centered', backgrounds: { default: 'dark' } },
  tags: ['autodocs'],
} satisfies Meta<typeof AgentCard>

export default meta
type Story = StoryObj<typeof meta>

const base = {
  id: 'agent-001',
  name: 'Alex (CEO)',
  role: 'ceo' as const,
  provider: 'anthropic',
  model: 'claude-opus-4-7',
  createdAt: new Date().toISOString(),
}

export const Active: Story = {
  args: { agent: { ...base, status: 'active', currentTask: 'Defining company mission and setting Q1 KPIs' } },
}

export const WaitingApproval: Story = {
  args: { agent: { ...base, status: 'waiting_approval', currentTask: 'Waiting for approval to send email to client@example.com' } },
}

export const Sleeping: Story = {
  args: { agent: { ...base, name: 'Weather Monitor', role: 'developer', status: 'sleeping', currentTask: 'Scheduled: 07:00 daily weather check' } },
}

export const Idle: Story = {
  args: { agent: { ...base, status: 'idle' } },
}

export const Terminated: Story = {
  args: { agent: { ...base, status: 'terminated' } },
}
