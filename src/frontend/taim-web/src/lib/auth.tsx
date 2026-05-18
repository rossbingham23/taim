import { createContext, useContext, useState, useCallback, useEffect, type ReactNode } from 'react'
import { connectSignalR, disconnectSignalR } from './signalr'

interface AuthState {
  token: string | null
  isAuthenticated: boolean
}

interface AuthContextValue extends AuthState {
  signIn: (token: string) => Promise<void>
  signOut: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(() => localStorage.getItem('taim_token'))

  // Reconnect SignalR when app loads with a saved token
  useEffect(() => {
    if (token) connectSignalR(token).catch(() => {})
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const signIn = useCallback(async (newToken: string) => {
    localStorage.setItem('taim_token', newToken)
    setToken(newToken)
    await connectSignalR(newToken)
  }, [])

  const signOut = useCallback(() => {
    localStorage.removeItem('taim_token')
    setToken(null)
    disconnectSignalR()
  }, [])

  return (
    <AuthContext.Provider value={{ token, isAuthenticated: !!token, signIn, signOut }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
