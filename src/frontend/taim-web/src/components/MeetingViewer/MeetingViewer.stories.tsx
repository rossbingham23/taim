import type { Meta, StoryObj } from '@storybook/react'
import { MeetingViewer } from './MeetingViewer'
import type { MeetingRecord, MeetingMessage, Agent } from '../../lib/types'

const meta = {
  title: 'Components/MeetingViewer',
  component: MeetingViewer,
  parameters: { layout: 'padded', backgrounds: { default: 'dark' } },
  tags: ['autodocs'],
} satisfies Meta<typeof MeetingViewer>

export default meta
type Story = StoryObj<typeof meta>

const agents: Agent[] = [
  { id: 'ceo-1', name: 'Alex (CEO)', role: 'ceo', status: 'idle', provider: 'anthropic', model: 'claude-3-5-sonnet', createdAt: new Date().toISOString() },
  { id: 'cmo-1', name: 'Maya (CMO)', role: 'cmo', status: 'idle', provider: 'anthropic', model: 'claude-3-5-sonnet', createdAt: new Date().toISOString() },
  { id: 'cto-1', name: 'Sam (CTO)', role: 'cto', status: 'idle', provider: 'anthropic', model: 'claude-3-5-sonnet', createdAt: new Date().toISOString() },
]

const meeting: MeetingRecord = {
  id: '1', tenantId: 't1', taskId: 'task1',
  topic: 'Q1 Go-to-Market Strategy', meetingType: 'kickoff_sync', status: 'completed',
  organizerAgentId: 'ceo-1', participantAgentIds: ['cmo-1', 'cto-1'],
  summary: 'Team aligned on LinkedIn-led content strategy. CTO owns outreach automation; CMO finalizes message templates.',
  messageCount: 4,
  startedAt: new Date(Date.now() - 300000).toISOString(),
  completedAt: new Date(Date.now() - 60000).toISOString(),
}

const messages: MeetingMessage[] = [
  { id: '1', meetingId: '1', speakerAgentId: 'ceo-1', content: 'We need to decide on our primary acquisition channel for Q1. What does each team think?', sequence: 0, createdAt: new Date(Date.now() - 300000).toISOString() },
  { id: '2', meetingId: '1', speakerAgentId: 'cmo-1', content: 'Based on market analysis, LinkedIn outreach gives us the best CAC for B2B consulting.', sequence: 1, createdAt: new Date(Date.now() - 240000).toISOString() },
  { id: '3', meetingId: '1', speakerAgentId: 'cto-1', content: 'We can automate the outreach pipeline with our existing GitHub connector. I can have it running within 48 hours.', sequence: 2, createdAt: new Date(Date.now() - 180000).toISOString() },
  { id: '4', meetingId: '1', speakerAgentId: 'ceo-1', content: 'Agreed. Maya, please finalize the message templates. Sam, get the automation running.', sequence: 3, createdAt: new Date(Date.now() - 120000).toISOString() },
]

export const Completed: Story = {
  args: { meeting, messages, agents, onClose: () => {} },
}

export const InProgress: Story = {
  args: { meeting: { ...meeting, status: 'in_progress', completedAt: undefined, summary: undefined }, messages: messages.slice(0, 2), agents, onClose: () => {} },
}
