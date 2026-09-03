import App from '../App'
import { appPath } from '../appPaths'
import { useAuth } from '../auth'
import { legalDocumentFromPath } from '../legal'
import { LegalAcceptanceBoundary } from './LegalAcceptanceBoundary'
import { LegalLinks } from './LegalLinks'
import { LegalPage } from './LegalPage'

export function AuthBoundary() {
  const auth = useAuth()
  const legalDocument = legalDocumentFromPath(window.location.pathname)

  if (legalDocument) return <LegalPage kind={legalDocument} />

  if (!auth.ready) {
    return <main className="auth-loading" aria-live="polite"><img src={appPath('logo.svg')} alt="" /><span>Préparation de Mail Manager…</span></main>
  }

  if (auth.authenticated) return <LegalAcceptanceBoundary><App /></LegalAcceptanceBoundary>

  return (
    <main className="auth-page">
      <header className="auth-brand"><img src={appPath('logo.svg')} alt="" /><div><strong>Mail Manager</strong><small>Classement automatique</small></div></header>
      <section className="auth-hero">
        <div className="auth-copy">
          <p className="overline">Votre boîte, enfin lisible</p>
          <h1>Les emails arrivent.<br />Ils savent déjà où aller.</h1>
          <p>Reliez Gmail ou Outlook, décrivez vos règles avec des mots simples et laissez Mail Manager ranger les nouveaux messages au bon endroit.</p>
          <div className="auth-actions">
            <button className="button primary" type="button" onClick={() => void auth.login()}>Se connecter →</button>
            <button className="button secondary" type="button" onClick={() => void auth.register()}>Créer un compte</button>
          </div>
        </div>
        <aside className="auth-demo-card">
          <span className="auth-demo-index">01</span>
          <p className="overline">Découverte libre</p>
          <h2>Essayez le moteur sans connecter de boîte.</h2>
          <p>Le profil de démonstration contient des destinations et des règles d’exemple. Vous pouvez simuler des emails, sans compte et sans accès à une messagerie réelle.</p>
          <button className="button demo-button" type="button" onClick={() => void auth.tryDemo()}>Ouvrir la démonstration <span>↗</span></button>
          <small>Données partagées et lecture seule · aucune donnée personnelle requise</small>
        </aside>
      </section>
      <footer className="auth-footer"><LegalLinks className="auth-legal-links" /><span>Gmail · Outlook · n8n</span></footer>
    </main>
  )
}
