import { useState } from 'react'
import type { GmailOAuthConfiguration, GmailSyncResult, Mailbox } from '../types'

type Props = {
  mailbox: Mailbox
  configuration: GmailOAuthConfiguration
  busy: boolean
  syncResult?: GmailSyncResult
  onConnect: () => void
  onTestConnection: () => void
  onSync: (maxResults: number) => void
  onDisconnect: () => void
}

const formatDate = (value?: string) => value
  ? new Intl.DateTimeFormat('fr-FR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
  : 'Jamais'

export function MailboxConnectionView({
  mailbox,
  configuration,
  busy,
  syncResult,
  onConnect,
  onTestConnection,
  onSync,
  onDisconnect,
}: Props) {
  const [maxResults, setMaxResults] = useState(5)
  const [confirmDisconnect, setConfirmDisconnect] = useState(false)

  return (
    <div className="page mailbox-page">
      <div className="page-header">
        <div>
          <p className="overline">Fournisseur de messagerie</p>
          <h1>Boîte Gmail</h1>
          <p>Connectez votre compte puis contrôlez le classement automatique de vos nouveaux emails.</p>
        </div>
        <span className={mailbox.isConnected ? 'status-pill active' : 'status-pill inactive'}>
          {mailbox.isConnected ? 'Connectée' : 'Non connectée'}
        </span>
      </div>

      {!configuration.isConfigured && (
        <section className="surface sync-error" role="status">
          <strong>Connexion Gmail temporairement indisponible</strong>
          <p>La configuration Google de Mail Manager doit être installée par l’administrateur du serveur.</p>
        </section>
      )}

      <div className="mailbox-grid">
        <section className="surface connection-card">
          <div className="gmail-mark" aria-hidden="true">M</div>
          <div className="connection-card-main">
            <p className="overline">Compte principal</p>
            <h2>{mailbox.emailAddress ?? 'Aucun compte Gmail connecté'}</h2>
            <p>
              {mailbox.isConnected
                ? 'Mail Manager surveille les nouveaux emails reçus et applique les labels décidés par vos règles.'
                : 'La connexion utilise la page de consentement officielle Google. Votre mot de passe n’est jamais transmis à Mail Manager.'}
            </p>

            <div className="connection-actions">
              {!mailbox.isConnected ? (
                <button className="button primary" type="button" disabled={busy || !configuration.isConfigured} onClick={onConnect}>
                  Connecter mon compte Gmail →
                </button>
              ) : (
                <>
                  <button className="button secondary" type="button" disabled={busy} onClick={onTestConnection}>Vérifier la connexion</button>
                  {!confirmDisconnect ? (
                    <button className="button text danger-text" type="button" disabled={busy} onClick={() => setConfirmDisconnect(true)}>Déconnecter</button>
                  ) : (
                    <div className="inline-confirm">
                      <span>Révoquer l’accès ?</span>
                      <button className="button danger" type="button" disabled={busy} onClick={onDisconnect}>Confirmer</button>
                      <button className="button text" type="button" onClick={() => setConfirmDisconnect(false)}>Annuler</button>
                    </div>
                  )}
                </>
              )}
            </div>
          </div>
        </section>

        <aside className="surface connection-details">
          <p className="overline">État</p>
          <dl>
            <div><dt>Fournisseur</dt><dd>Gmail</dd></div>
            <div><dt>Configuration OAuth</dt><dd>{configuration.isConfigured ? 'Prête' : 'À configurer'}</dd></div>
            <div><dt>Connectée le</dt><dd>{formatDate(mailbox.connectedAt)}</dd></div>
            <div><dt>Dernière synchronisation</dt><dd>{formatDate(mailbox.lastSyncAt)}</dd></div>
            <div><dt>Permission</dt><dd>Lecture et labels</dd></div>
          </dl>
          {mailbox.lastSyncError && <div className="sync-error"><strong>Dernière erreur</strong><p>{mailbox.lastSyncError}</p></div>}
        </aside>
      </div>

      <section className={mailbox.isConnected ? 'surface real-sync-card' : 'surface real-sync-card disabled-section'}>
        <div className="section-heading">
          <div><p className="overline">Classement automatique</p><h2>Surveillance des nouveaux emails</h2><p>n8n vérifie automatiquement la boîte chaque minute. Chaque nouveau message est évalué par les règles et reçoit le label Gmail gagnant.</p></div>
          <div className={mailbox.isConnected ? 'automation-status active' : 'automation-status'}><span aria-hidden="true" />{mailbox.isConnected ? 'Surveillance active' : 'Connexion requise'}</div>
        </div>

        <div className="manual-sync-row">
          <div><strong>Vérification immédiate</strong><p>Utile pour tester sans attendre le prochain passage automatique.</p></div>
          <div className="sync-controls">
            <label htmlFor="gmail-max-results">Maximum</label>
            <select id="gmail-max-results" value={maxResults} disabled={!mailbox.isConnected || busy} onChange={(event) => setMaxResults(Number(event.target.value))}>
              {[1, 3, 5, 10, 20].map((value) => <option key={value} value={value}>{value} email{value > 1 ? 's' : ''}</option>)}
            </select>
            <button className="button primary" type="button" disabled={!mailbox.isConnected || busy} onClick={() => onSync(maxResults)}>
              {busy ? 'Traitement…' : 'Vérifier maintenant →'}
            </button>
          </div>
        </div>

        <div className="privacy-note"><span aria-hidden="true">◆</span><p><strong>Confidentialité :</strong> le corps sert uniquement à la décision en mémoire. Il n’est ni conservé en base ni envoyé dans les données d’exécution n8n.</p></div>

        {syncResult && (
          <div className="sync-result" aria-live="polite">
            <div className="sync-metrics">
              <article><strong>{syncResult.processedCount}</strong><span>traités</span></article>
              <article><strong>{syncResult.classifiedCount}</strong><span>classés</span></article>
              <article><strong>{syncResult.labelAppliedCount}</strong><span>labels appliqués</span></article>
              <article><strong>{syncResult.failureCount}</strong><span>erreurs</span></article>
            </div>
            {syncResult.processedCount === 0 ? (
              <p className="empty-sync">Aucun nouvel email à traiter parmi les {syncResult.discoveredCount} détectés.</p>
            ) : (
              <div className="sync-result-list">
                {syncResult.results.map((item) => (
                  <article key={item.externalMessageId}>
                    <span className={item.error ? 'result-dot error' : item.isClassified ? 'result-dot classified' : 'result-dot'} />
                    <div><code>{item.externalMessageId}</code><strong>{item.error ?? item.label?.name ?? 'Non classé'}</strong><small>{item.matchedCriteria.join(' · ') || item.noMatchReason}</small></div>
                    {item.labelApplied && <span className="status-pill active">Label appliqué</span>}
                  </article>
                ))}
              </div>
            )}
          </div>
        )}
      </section>
    </div>
  )
}
