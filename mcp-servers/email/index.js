/**
 * Email MCP Server
 *
 * Exposes tools:
 *   - send_email: Send an email via SMTP
 *   - read_inbox: List recent emails (IMAP — not yet implemented)
 *
 * Configure via environment variables:
 *   SMTP_HOST, SMTP_PORT, SMTP_USER, SMTP_PASS, SMTP_FROM
 *
 * This tool is wrapped with ApprovalRequiredAIFunction on the agent side,
 * so every send_email call requires explicit user approval.
 */

import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js'
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js'
import nodemailer from 'nodemailer'
import { z } from 'zod'

const server = new McpServer({
  name: 'email',
  version: '0.1.0',
})

server.tool(
  'send_email',
  'Send an email to a recipient. Requires user approval before sending.',
  {
    to: z.string().email().describe('Recipient email address'),
    subject: z.string().describe('Email subject line'),
    body: z.string().describe('Plain text email body'),
    cc: z.string().email().optional().describe('CC email address'),
  },
  async ({ to, subject, body, cc }) => {
    const host = process.env.SMTP_HOST
    const port = parseInt(process.env.SMTP_PORT ?? '587', 10)
    const user = process.env.SMTP_USER
    const pass = process.env.SMTP_PASS
    const from = process.env.SMTP_FROM ?? user

    if (!host || !user || !pass) {
      return {
        content: [{ type: 'text', text: `[email stub] SMTP not configured. Would send: To=${to} Subject="${subject}"` }],
      }
    }

    const transporter = nodemailer.createTransport({ host, port, secure: port === 465, auth: { user, pass } })
    await transporter.sendMail({ from, to, cc, subject, text: body })

    return { content: [{ type: 'text', text: `Email sent successfully to ${to}.` }] }
  }
)

server.tool(
  'read_inbox',
  'List recent emails from the inbox (returns last N emails).',
  { limit: z.number().int().min(1).max(50).default(10).describe('Max number of emails to return') },
  async () => {
    return {
      content: [{ type: 'text', text: '[read_inbox] IMAP inbox reading not yet implemented.' }],
    }
  }
)

const transport = new StdioServerTransport()
await server.connect(transport)
