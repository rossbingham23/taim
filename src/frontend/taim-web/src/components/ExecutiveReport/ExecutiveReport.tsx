import type { ExecutiveReport as ExecutiveReportType } from '../../lib/types'

interface ExecutiveReportProps {
  report: ExecutiveReportType
}

export function ExecutiveReport({ report }: ExecutiveReportProps) {
  return (
    <div style={{ fontFamily: 'sans-serif', background: '#0f172a', borderRadius: 8, padding: '1.5rem', border: '1px solid #1e293b' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 16 }}>
        <div>
          <div style={{ fontWeight: 700, color: '#f1f5f9', fontSize: 16 }}>{report.title}</div>
          <div style={{ fontSize: 12, color: '#64748b', marginTop: 4 }}>
            {report.agentName} · {new Date(report.generatedAt).toLocaleString()}
          </div>
        </div>
      </div>
      <div
        style={{
          fontSize: 14, color: '#cbd5e1', lineHeight: 1.7,
          whiteSpace: 'pre-wrap',
          borderTop: '1px solid #1e293b', paddingTop: 16,
        }}
      >
        {report.content}
      </div>
    </div>
  )
}
