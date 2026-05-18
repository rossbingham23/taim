import type { Meta, StoryObj } from '@storybook/react'
import { BudgetMeter } from './BudgetMeter'

const meta = {
  title: 'Components/BudgetMeter',
  component: BudgetMeter,
  parameters: { layout: 'centered', backgrounds: { default: 'dark' } },
  tags: ['autodocs'],
} satisfies Meta<typeof BudgetMeter>

export default meta
type Story = StoryObj<typeof meta>

const agents = [
  { agentId: 'ceo', agentName: 'Alex (CEO)', totalCostUsd: 0.02, totalTokens: 45000 },
  { agentId: 'cto', agentName: 'Sam (CTO)', totalCostUsd: 0.015, totalTokens: 32000 },
  { agentId: 'dev', agentName: 'Dev Agent', totalCostUsd: 0.008, totalTokens: 18000 },
]

export const Healthy: Story = {
  args: { budget: { id: '1', limitUsd: 100, spentUsd: 4.32, status: 'active', byAgent: agents } },
}

export const Warning: Story = {
  args: { budget: { id: '1', limitUsd: 100, spentUsd: 84.50, status: 'active', byAgent: agents } },
}

export const Exhausted: Story = {
  args: { budget: { id: '1', limitUsd: 100, spentUsd: 100.00, status: 'exhausted', byAgent: agents } },
}
