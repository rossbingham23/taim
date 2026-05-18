import type { Meta, StoryObj } from '@storybook/react'
import { ExecutiveReport } from './ExecutiveReport'

const meta = {
  title: 'Components/ExecutiveReport',
  component: ExecutiveReport,
  parameters: { layout: 'padded', backgrounds: { default: 'dark' } },
  tags: ['autodocs'],
} satisfies Meta<typeof ExecutiveReport>

export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {
  args: {
    report: {
      id: '1',
      agentId: 'ceo',
      agentName: 'Alex (CEO)',
      title: 'Week 1 Executive Summary',
      generatedAt: new Date().toISOString(),
      content: `## Progress

The consulting company has officially launched. Key milestones achieved this week:

- Company mission statement drafted and approved
- Executive team onboarded: CTO (Sam), CMO (Maya), CFO (Jordan), HR (Riley)
- Go-to-market strategy finalized for Q1
- Initial client outreach pipeline automated (50 contacts/day)

## KPI Status

- Customer Satisfaction Target: 90% — Baseline not yet measured
- Pipeline Leads: 12 of 50 target acquired (24%)
- Zero Critical Bugs: Met (no production system yet)

## Next Week Focus

1. Complete website build (Dev team)
2. Launch LinkedIn content strategy (Maya)
3. Establish financial tracking (Jordan)
4. Begin first client discovery calls`,
    },
  },
}
