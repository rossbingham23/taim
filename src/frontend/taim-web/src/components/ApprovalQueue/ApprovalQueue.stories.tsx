import type { Meta, StoryObj } from '@storybook/react'
import { ApprovalQueue } from './ApprovalQueue'
import type { ApprovalRequest } from '../../lib/types'

const meta = {
  title: 'Components/ApprovalQueue',
  component: ApprovalQueue,
  parameters: { layout: 'padded', backgrounds: { default: 'dark' } },
  tags: ['autodocs'],
} satisfies Meta<typeof ApprovalQueue>

export default meta
type Story = StoryObj<typeof meta>

const requests: ApprovalRequest[] = [
  {
    id: '1', agentId: 'cmo', agentName: 'Maya (CMO)', toolName: 'send_email',
    toolDescription: 'Send an email',
    parameters: { to: 'client@example.com', subject: 'Q1 Proposal', body: 'Dear Client...' },
    status: 'pending', requestedAt: new Date().toISOString(),
  },
  {
    id: '2', agentId: 'dev', agentName: 'Dev Agent', toolName: 'github_create_pr',
    toolDescription: 'Create a pull request',
    parameters: { repo: 'acme/website', title: 'feat: landing page redesign', base: 'main' },
    status: 'pending', requestedAt: new Date().toISOString(),
  },
]

export const WithRequests: Story = {
  args: { requests, onDecide: (id, approved, scope) => console.log(id, approved, scope) },
}

export const Empty: Story = {
  args: { requests: [], onDecide: () => {} },
}
