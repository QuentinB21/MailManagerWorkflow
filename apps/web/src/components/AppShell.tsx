import type { ReactNode } from 'react'
import type { Mailbox } from '../types'

export type AppView = 'dashboard' | 'mailbox' | 'configuration' | 'test'

type Props = {
  activeView: AppView
  mailbox?: Mailbox
  onNavigate: (view: AppView) => void
  children: ReactNode
}

const navItems: Array<{ id: AppView; label: string; description: string }> = [
  { id: 'dashboard', label: 'Tableau de bord', description: 'Vue d’ensemble' },
  { id: 'mailbox', label: 'Boîte Gmail', description: 'Connexion et synchronisation' },
  { id: 'configuration', label: 'Configuration', description: 'Labels et règles' },
  { id: 'test', label: 'Tester le workflow', description: 'Email fictif' },
]

function NavIcon({ view }: { view: AppView }) {
  if (view === 'mailbox') {
    return <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 6h16v12H4zM4 7l8 6 8-6" /></svg>
  }
  if (view === 'configuration') {
    return <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 7h10M18 7h2M4 17h2M10 17h10M14 4v6M6 14v6" /></svg>
  }
  if (view === 'test') {
    return <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m8 5 11 7-11 7V5Z" /></svg>
  }
  return <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 4h6v6H4zM14 4h6v10h-6zM4 14h6v6H4zM14 18h6v2h-6z" /></svg>
}

export function AppShell({ activeView, mailbox, onNavigate, children }: Props) {
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">M</span>
          <div><strong>Mail Manager</strong><small>Workflow Lab</small></div>
        </div>

        <nav className="main-nav" aria-label="Navigation principale">
          {navItems.map((item) => (
            <button
              key={item.id}
              type="button"
              className={activeView === item.id ? 'nav-item active' : 'nav-item'}
              onClick={() => onNavigate(item.id)}
              aria-current={activeView === item.id ? 'page' : undefined}
            >
              <span className="nav-icon"><NavIcon view={item.id} /></span>
              <span><strong>{item.label}</strong><small>{item.description}</small></span>
            </button>
          ))}
        </nav>

        <div className="sidebar-footer">
          <span className={mailbox?.isConnected ? 'connection-dot' : 'connection-dot offline'} />
          <div><strong>{mailbox?.emailAddress ?? mailbox?.displayName ?? 'Connexion…'}</strong><small>{mailbox?.isConnected ? 'Gmail connecté' : 'Gmail non connecté'}</small></div>
        </div>
      </aside>

      <div className="app-content">
        <header className="mobile-header">
          <div className="brand"><span className="brand-mark">M</span><strong>Mail Manager</strong></div>
          <span className={mailbox?.isConnected ? 'connection-dot' : 'connection-dot offline'} />
        </header>
        <div className="mobile-nav" aria-label="Navigation mobile">
          {navItems.map((item) => <button key={item.id} className={activeView === item.id ? 'active' : ''} onClick={() => onNavigate(item.id)}>{item.label}</button>)}
        </div>
        {children}
      </div>
    </div>
  )
}
