import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { login } from '../../lib/api'
import { useAuth } from '../../lib/auth'

export function Login() {
  const { signIn } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      const res = await login(email, password)
      await signIn(res.token)
      navigate('/')
    } catch {
      setError('Invalid email or password.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div style={styles.root}>
      <div style={styles.card}>
        <div style={styles.logo}>TAIM</div>
        <div style={styles.subtitle}>Team AI Manager</div>
        <form onSubmit={handleSubmit} style={styles.form}>
          <input
            type="email"
            placeholder="Email"
            value={email}
            onChange={e => setEmail(e.target.value)}
            required
            style={styles.input}
          />
          <input
            type="password"
            placeholder="Password"
            value={password}
            onChange={e => setPassword(e.target.value)}
            required
            style={styles.input}
          />
          {error && <div style={styles.error}>{error}</div>}
          <button type="submit" disabled={loading} style={styles.button}>
            {loading ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      </div>
    </div>
  )
}

const styles = {
  root: {
    minHeight: '100vh',
    background: '#0f172a',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontFamily: 'system-ui, sans-serif',
  } as const,
  card: {
    background: '#1e293b',
    borderRadius: 12,
    padding: '2.5rem',
    width: 360,
    border: '1px solid #334155',
  } as const,
  logo: {
    fontSize: 28,
    fontWeight: 800,
    color: '#60a5fa',
    letterSpacing: 4,
    marginBottom: 4,
  } as const,
  subtitle: {
    fontSize: 13,
    color: '#64748b',
    marginBottom: 28,
  } as const,
  form: {
    display: 'flex',
    flexDirection: 'column' as const,
    gap: 12,
  },
  input: {
    background: '#0f172a',
    border: '1px solid #334155',
    borderRadius: 6,
    padding: '10px 14px',
    color: '#f1f5f9',
    fontSize: 14,
    outline: 'none',
    width: '100%',
    boxSizing: 'border-box' as const,
  },
  error: {
    color: '#ef4444',
    fontSize: 13,
  },
  button: {
    background: '#3b82f6',
    color: '#fff',
    border: 'none',
    borderRadius: 6,
    padding: '10px 0',
    fontSize: 14,
    fontWeight: 600,
    cursor: 'pointer',
    marginTop: 4,
  } as const,
}
