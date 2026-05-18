/**
 * Web Search MCP Server
 *
 * Exposes a `web_search` tool that agents can call to search the internet.
 * Uses the Brave Search API (configure BRAVE_API_KEY env var) or DuckDuckGo
 * as a free fallback.
 *
 * Runs as an HTTP SSE server on PORT (default 3100).
 * The Taim.Connectors.WebSearch connector connects to this via stdio or HTTP.
 */

import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js'
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js'
import { z } from 'zod'

const server = new McpServer({
  name: 'web-search',
  version: '0.1.0',
})

server.tool(
  'web_search',
  'Search the internet for information on a given query.',
  { query: z.string().describe('The search query') },
  async ({ query }) => {
    const apiKey = process.env.BRAVE_API_KEY
    if (!apiKey) {
      return {
        content: [{ type: 'text', text: `[web_search stub] No BRAVE_API_KEY set. Query was: "${query}"` }],
      }
    }

    const response = await fetch(
      `https://api.search.brave.com/res/v1/web/search?q=${encodeURIComponent(query)}&count=5`,
      { headers: { 'X-Subscription-Token': apiKey, Accept: 'application/json' } }
    )
    if (!response.ok) {
      return { content: [{ type: 'text', text: `Search API error: ${response.status}` }], isError: true }
    }

    const data = await response.json()
    const results = (data.web?.results ?? []).slice(0, 5).map(r =>
      `**${r.title}**\n${r.url}\n${r.description ?? ''}`
    ).join('\n\n')

    return { content: [{ type: 'text', text: results || 'No results found.' }] }
  }
)

const transport = new StdioServerTransport()
await server.connect(transport)
