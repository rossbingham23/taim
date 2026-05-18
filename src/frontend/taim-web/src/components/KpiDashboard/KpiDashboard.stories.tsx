import type { Meta, StoryObj } from '@storybook/react'
import { KpiDashboard } from './KpiDashboard'
import type { KpiNode } from '../../lib/types'

const meta = {
  title: 'Components/KpiDashboard',
  component: KpiDashboard,
  parameters: { layout: 'padded', backgrounds: { default: 'dark' } },
  tags: ['autodocs'],
} satisfies Meta<typeof KpiDashboard>

export default meta
type Story = StoryObj<typeof meta>

const tree: KpiNode[] = [
  {
    id: '1', agentId: 'ceo', agentName: 'Alex (CEO)', name: 'Customer Satisfaction', description: '', unit: '%',
    direction: 'higher_better', targetValue: 90, currentValue: 72, updatedAt: new Date().toISOString(),
    children: [
      {
        id: '2', agentId: 'cto', agentName: 'Sam (CTO)', name: 'Zero Critical Bugs', description: '', unit: 'bugs',
        direction: 'lower_better', targetValue: 0, currentValue: 1, updatedAt: new Date().toISOString(),
        children: [
          {
            id: '3', agentId: 'qa', agentName: 'QA Bot', name: 'Test Coverage', description: '', unit: '%',
            direction: 'higher_better', targetValue: 95, currentValue: 88, updatedAt: new Date().toISOString(),
            children: [],
          },
        ],
      },
    ],
  },
]

export const WithData: Story = {
  args: { roots: tree },
}

export const Empty: Story = {
  args: { roots: [] },
}
