import Keycloak from 'keycloak-js'
import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'

const keycloak = new Keycloak({
  url: import.meta.env.VITE_KEYCLOAK_URL ?? 'http://localhost:8081',
  realm: import.meta.env.VITE_KEYCLOAK_REALM ?? 'mail-manager',
  clientId: import.meta.env.VITE_KEYCLOAK_CLIENT_ID ?? 'mail-manager-web',
})

let initialization: Promise<boolean> | undefined

const initialize = () => {
  initialization ??= keycloak.init({
    onLoad: 'check-sso',
    pkceMethod: 'S256',
    checkLoginIframe: false,
  })
  return initialization
}

export async function getAccessToken() {
  if (!keycloak.authenticated) return undefined
  await keycloak.updateToken(30)
  return keycloak.token
}

type AuthContextValue = {
  ready: boolean
  authenticated: boolean
  displayName: string
  username: string
  isDemo: boolean
  login: () => Promise<void>
  register: () => Promise<void>
  tryDemo: () => Promise<void>
  logout: () => Promise<void>
  manageAccount: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false)
  const [authenticated, setAuthenticated] = useState(false)

  useEffect(() => {
    let active = true
    keycloak.onAuthSuccess = () => active && setAuthenticated(true)
    keycloak.onAuthLogout = () => active && setAuthenticated(false)
    keycloak.onAuthRefreshError = () => void keycloak.login({ redirectUri: window.location.origin })
    keycloak.onTokenExpired = () => void keycloak.updateToken(30).catch(() => keycloak.login())
    initialize()
      .then((value) => active && setAuthenticated(value))
      .finally(() => active && setReady(true))
    return () => { active = false }
  }, [])

  const value = useMemo<AuthContextValue>(() => {
    const token = keycloak.tokenParsed
    const roles = token?.realm_access?.roles ?? []
    return {
      ready,
      authenticated,
      displayName: token?.name ?? token?.preferred_username ?? 'Utilisateur',
      username: token?.preferred_username ?? '',
      isDemo: roles.includes('demo'),
      login: () => keycloak.login({ redirectUri: window.location.origin, locale: 'fr' }),
      register: () => keycloak.register({ redirectUri: window.location.origin, locale: 'fr' }),
      tryDemo: () => keycloak.login({ redirectUri: window.location.origin, loginHint: 'demo', locale: 'fr', prompt: 'login' }),
      logout: () => keycloak.logout({ redirectUri: window.location.origin }),
      manageAccount: () => keycloak.accountManagement(),
    }
  }, [authenticated, ready])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth doit être utilisé dans AuthProvider.')
  return context
}

