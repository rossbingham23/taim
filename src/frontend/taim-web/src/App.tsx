import { BrowserRouter, Routes, Route, NavLink, Navigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './lib/auth'
import { Login } from './features/auth/Login'
import { TaskIntake } from './features/task-intake/TaskIntake'
import { TeamView } from './features/team-view/TeamView'
import { Approvals } from './features/approvals/Approvals'
import { Reports } from './features/reports/Reports'
import { Settings } from './features/settings/Settings'
import { Console } from './features/console/Console'
import { KpiPage } from './features/kpi/KpiPage'
import type { ReactNode } from 'react'

function ProtectedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />
}

function Shell({ children }: { children: ReactNode }) {
  const { signOut } = useAuth()
  return (
    <div style={styles.shell}>
      <nav style={styles.nav}>
        <div style={styles.navLogo}>TAIM</div>
        <div style={styles.navLinks}>
          <NavItem to="/" label="Goals" exact />
          <NavItem to="/approvals" label="Approvals" />
          <NavItem to="/reports" label="Reports" />
          <NavItem to="/console" label="Console" />
          <NavItem to="/settings" label="Settings" />
        </div>
        <button onClick={signOut} style={styles.signOutBtn}>Sign out</button>
      </nav>
      <main style={styles.main}>{children}</main>
    </div>
  )
}

function NavItem({ to, label, exact }: { to: string; label: string; exact?: boolean }) {
  return (
    <NavLink
      to={to}
      end={exact}
      style={({ isActive }) => ({
        ...styles.navLink,
        ...(isActive ? styles.navLinkActive : {}),
      })}
    >
      {label}
    </NavLink>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route
            path="/*"
            element={
              <ProtectedRoute>
                <Shell>
                  <Routes>
                    <Route path="/" element={<TaskIntake />} />
                    <Route path="/tasks/:taskId" element={<TeamView />} />
                    <Route path="/tasks/:taskId/kpis" element={<KpiPage />} />
                    <Route path="/approvals" element={<Approvals />} />
                    <Route path="/reports" element={<Reports />} />
                    <Route path="/console" element={<Console />} />
                    <Route path="/settings" element={<Settings />} />
                  </Routes>
                </Shell>
              </ProtectedRoute>
            }
          />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}

const styles = {
  shell: {
    display: 'flex',
    minHeight: '100vh',
    background: '#0f172a',
    color: '#f1f5f9',
  } as const,
  nav: {
    width: 200,
    flexShrink: 0,
    background: '#0f172a',
    borderRight: '1px solid #1e293b',
    display: 'flex',
    flexDirection: 'column' as const,
    padding: '1.5rem 0',
  },
  navLogo: {
    fontSize: 20,
    fontWeight: 800,
    color: '#60a5fa',
    letterSpacing: 4,
    padding: '0 1.25rem',
    marginBottom: '2rem',
  },
  navLinks: {
    display: 'flex',
    flexDirection: 'column' as const,
    flex: 1,
    gap: 2,
  },
  navLink: {
    display: 'block',
    padding: '8px 1.25rem',
    color: '#64748b',
    textDecoration: 'none',
    fontSize: 14,
    fontWeight: 500,
    borderRadius: 0,
    transition: 'color 0.1s',
    fontFamily: 'system-ui, sans-serif',
  },
  navLinkActive: {
    color: '#f1f5f9',
    background: '#1e293b',
  },
  signOutBtn: {
    margin: '0 1rem',
    background: 'none',
    border: 'none',
    color: '#475569',
    fontSize: 13,
    cursor: 'pointer',
    textAlign: 'left' as const,
    padding: '8px 0.25rem',
    fontFamily: 'system-ui, sans-serif',
  },
  main: {
    flex: 1,
    padding: '2rem 2.5rem',
    overflow: 'auto',
    minWidth: 0,
  },
}
