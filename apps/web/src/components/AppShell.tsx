import type { ReactNode } from 'react'
import type { Mailbox } from '../types'

export type AppView = 'classification' | 'activity' | 'settings'

type Props = {
  activeView: AppView
  mailbox?: Mailbox
  mailboxes?: Mailbox[]
  onSelectMailbox?: (mailboxId: string) => void
  onNavigate: (view: AppView) => void
  children: ReactNode
}

const navItems: Array<{ id: AppView; label: string }> = [
  { id: 'classification', label: 'Classement' },
  { id: 'activity', label: 'Activité' },
  { id: 'settings', label: 'Paramètres' },
]

export function AppShell({ activeView, mailbox, mailboxes = [], onSelectMailbox, onNavigate, children }: Props) {
  return (
    <div className="app-shell">
      <header className="topbar">
        <button className="brand brand-button" type="button" onClick={() => onNavigate('classification')} aria-label="Ouvrir le classement">
          <img className="brand-mark" src="/logo.svg" alt="" aria-hidden="true" />
          <div><strong>Mail Manager</strong><small>Classement automatique</small></div>
        </button>
        <nav className="main-nav" aria-label="Navigation principale">
          {navItems.map((item) => <button key={item.id} type="button" className={activeView === item.id ? 'nav-item active' : 'nav-item'} onClick={() => onNavigate(item.id)} aria-current={activeView === item.id ? 'page' : undefined}>{item.label}</button>)}
        </nav>
        <label className="mailbox-switcher">
          <span className="sr-only">Boîte active</span>
          <select value={mailbox?.id ?? ''} onChange={(event) => onSelectMailbox?.(event.target.value)} aria-label="Boîte active">
            {mailboxes.map((item) => <option key={item.id} value={item.id}>{item.emailAddress ?? item.displayName} · {item.provider}{item.requiresReconnect ? ' · Reconnexion nécessaire' : ''}</option>)}
          </select>
        </label>
      </header>
      <main className="app-content">{children}</main>
    </div>
  )
}
