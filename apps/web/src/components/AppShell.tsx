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

const navItems: Array<{ id: AppView; label: string; mobileLabel: string; icon: ReactNode }> = [
  { id: 'classification', label: 'Classement', mobileLabel: 'Classer', icon: <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 6h16M4 12h10M4 18h7" /><path d="m16 16 2 2 3-4" /></svg> },
  { id: 'activity', label: 'Activité', mobileLabel: 'Activité', icon: <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 4v16h15" /><path d="m8 15 3-4 3 2 4-6" /></svg> },
  { id: 'settings', label: 'Paramètres', mobileLabel: 'Boîtes', icon: <svg viewBox="0 0 24 24" aria-hidden="true"><rect x="4" y="6" width="16" height="13" rx="2" /><path d="m5 8 7 5 7-5" /></svg> },
]

export function AppShell({ activeView, mailbox, mailboxes = [], onSelectMailbox, onNavigate, children }: Props) {
  return (
    <div className="app-shell">
      <header className="topbar">
        <button className="brand brand-button" type="button" onClick={() => onNavigate('classification')} aria-label="Ouvrir le classement">
          <img className="brand-mark" src="/logo.svg" alt="" aria-hidden="true" />
          <div><strong>Mail Manager</strong><small>Classement automatique</small></div>
        </button>
        <nav className="main-nav" aria-label="Navigation principale" data-active={activeView}>
          {navItems.map((item) => <button key={item.id} type="button" className={activeView === item.id ? 'nav-item active' : 'nav-item'} onClick={() => onNavigate(item.id)} aria-current={activeView === item.id ? 'page' : undefined}><span className="nav-icon">{item.icon}</span><span className="nav-label desktop-label">{item.label}</span><span className="nav-label mobile-label">{item.mobileLabel}</span></button>)}
          <span className="nav-indicator" aria-hidden="true" />
        </nav>
        <label className="mailbox-switcher">
          <span className="sr-only">Boîte active</span>
          <select value={mailbox?.id ?? ''} onChange={(event) => onSelectMailbox?.(event.target.value)} aria-label="Boîte active">
            {mailboxes.map((item) => <option key={item.id} value={item.id}>{item.emailAddress ?? item.displayName} · {item.provider}{item.requiresReconnect ? ' · Reconnexion nécessaire' : ''}</option>)}
          </select>
        </label>
      </header>
      <main className="app-content"><div className="view-frame" key={activeView}>{children}</div></main>
    </div>
  )
}
