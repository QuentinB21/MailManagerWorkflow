import { useState } from 'react'
import type { Mailbox, MailboxSyncResult, MailProvider, ProviderConfiguration } from '../types'

type Props = {
  mailboxes: Mailbox[]
  selectedMailbox?: Mailbox
  configurations: Record<MailProvider, ProviderConfiguration>
  busy: boolean
  syncResult?: MailboxSyncResult
  onSelect: (mailboxId: string) => void
  onAdd: (provider: MailProvider) => void
  onConnect: () => void
  onTestConnection: () => void
  onSync: (maxResults: number) => void
  onDisconnect: () => void
  onDelete: () => void
}

const formatDate = (value?: string) => value
  ? new Intl.DateTimeFormat('fr-FR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
  : 'Jamais'

export function MailboxConnectionView({
  mailboxes,
  selectedMailbox: mailbox,
  configurations,
  busy,
  syncResult,
  onSelect,
  onAdd,
  onConnect,
  onTestConnection,
  onSync,
  onDisconnect,
  onDelete,
}: Props) {
  const [maxResults, setMaxResults] = useState(5)
  const [confirmDisconnect, setConfirmDisconnect] = useState(false)

  if (!mailbox) return <div className="page"><p>Aucune boîte configurée.</p></div>
  const provider = mailbox.provider
  const isGmail = provider === 'Gmail'
  const configuration = configurations[provider]
  const destinationName = isGmail ? 'label' : 'catégorie'
  const isOperational = mailbox.isConnected && !mailbox.requiresReconnect
  const connectionStatus = mailbox.requiresReconnect
    ? 'reconnexion nécessaire'
    : mailbox.isConnected ? 'connectée' : 'à connecter'

  return (
    <div className="page mailbox-page">
      <div className="page-header mailbox-settings-header">
        <div><h1>Boîtes connectées</h1><p>Chaque boîte possède ses propres règles, destinations et historique.</p></div>
        <div className="add-mailbox-actions">
          <button className="button secondary" type="button" disabled={busy} onClick={() => onAdd('Gmail')}>+ Gmail</button>
          <button className="button primary" type="button" disabled={busy} onClick={() => onAdd('Outlook')}>+ Outlook</button>
        </div>
      </div>

      <div className="mailbox-tabs" role="tablist" aria-label="Boîtes configurées">
        {mailboxes.map((item) => (
          <button key={item.id} role="tab" aria-selected={item.id === mailbox.id} className={`${item.id === mailbox.id ? 'mailbox-tab active' : 'mailbox-tab'}${item.requiresReconnect ? ' reconnect' : ''}`} onClick={() => onSelect(item.id)}>
            <span className={`provider-dot ${item.provider.toLowerCase()}`} />
            <span><strong>{item.emailAddress ?? item.displayName}</strong><small>{item.provider} · {item.requiresReconnect ? 'reconnexion nécessaire' : item.isConnected ? 'connectée' : 'à connecter'}</small></span>
          </button>
        ))}
      </div>

      {!configuration?.isConfigured && (
        <section className="surface sync-error" role="status">
          <strong>Connexion {provider} temporairement indisponible</strong>
          <p>Les identifiants OAuth {isGmail ? 'Google' : 'Microsoft'} doivent être installés par l’administrateur du serveur.</p>
          {!isGmail && <a href="https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade" target="_blank" rel="noreferrer">Ouvrir les inscriptions d’applications Microsoft Entra ↗</a>}
        </section>
      )}

      <div className="mailbox-grid">
        <section className={`surface connection-card${mailbox.requiresReconnect ? ' reconnect-required' : ''}`}>
          <div className={isGmail ? 'provider-mark gmail' : 'provider-mark outlook'} aria-hidden="true">{isGmail ? 'M' : 'O'}</div>
          <div className="connection-card-main">
            <p className="overline">{provider}</p>
            <h2>{mailbox.emailAddress ?? `Aucun compte ${provider} connecté`}</h2>
            <p>{mailbox.requiresReconnect
              ? `L’autorisation ${provider} n’est plus valide. Reconnectez cette boîte pour reprendre le classement automatique.`
              : mailbox.isConnected
              ? `Mail Manager surveille cette boîte et applique les ${destinationName}s décidés par ses règles.`
              : `La connexion utilise la page de consentement officielle ${isGmail ? 'Google' : 'Microsoft'}. Votre mot de passe n’est jamais transmis à Mail Manager.`}</p>
            <div className="connection-actions">
              {!mailbox.isConnected ? (
                <>
                  <button className="button primary" type="button" disabled={busy || !configuration?.isConfigured} onClick={onConnect}>Connecter ce compte {provider} →</button>
                  <button className="button text danger-text" type="button" disabled={busy} onClick={onDelete}>Supprimer cette entrée</button>
                </>
              ) : mailbox.requiresReconnect ? (
                <>
                  <button className="button primary" type="button" disabled={busy || !configuration?.isConfigured} onClick={onConnect}>Reconnecter ce compte {provider} →</button>
                  {!confirmDisconnect ? (
                    <button className="button text danger-text" type="button" disabled={busy} onClick={() => setConfirmDisconnect(true)}>Déconnecter</button>
                  ) : (
                    <div className="inline-confirm"><span>Révoquer l’accès ?</span><button className="button danger" type="button" disabled={busy} onClick={onDisconnect}>Confirmer</button><button className="button text" type="button" onClick={() => setConfirmDisconnect(false)}>Annuler</button></div>
                  )}
                </>
              ) : (
                <>
                  <button className="button secondary" type="button" disabled={busy} onClick={onTestConnection}>Vérifier la connexion</button>
                  {!confirmDisconnect ? (
                    <button className="button text danger-text" type="button" disabled={busy} onClick={() => setConfirmDisconnect(true)}>Déconnecter</button>
                  ) : (
                    <div className="inline-confirm"><span>Révoquer l’accès ?</span><button className="button danger" type="button" disabled={busy} onClick={onDisconnect}>Confirmer</button><button className="button text" type="button" onClick={() => setConfirmDisconnect(false)}>Annuler</button></div>
                  )}
                </>
              )}
            </div>
          </div>
        </section>

        <aside className="surface connection-details">
          <p className="overline">État</p>
          <dl>
            <div><dt>Fournisseur</dt><dd>{provider}</dd></div>
            <div><dt>Configuration OAuth</dt><dd>{configuration?.isConfigured ? 'Prête' : 'À configurer'}</dd></div>
            <div><dt>État de la connexion</dt><dd className={mailbox.requiresReconnect ? 'reconnect-state' : undefined}>{connectionStatus}</dd></div>
            <div><dt>Connectée le</dt><dd>{formatDate(mailbox.connectedAt)}</dd></div>
            <div><dt>Dernière synchronisation</dt><dd>{formatDate(mailbox.lastSyncAt)}</dd></div>
            <div><dt>Classement</dt><dd>{isGmail ? 'Labels Gmail' : 'Catégories Outlook'}</dd></div>
          </dl>
          {mailbox.lastSyncError && <div className="sync-error"><strong>Dernière erreur</strong><p>{mailbox.lastSyncError}</p></div>}
        </aside>
      </div>

      <section className={isOperational ? 'surface real-sync-card' : 'surface real-sync-card disabled-section'}>
        <div className="section-heading">
          <div><p className="overline">Classement automatique</p><h2>Surveillance des nouveaux emails</h2><p>n8n vérifie automatiquement cette boîte chaque minute. Le moteur utilise uniquement les règles rattachées à cette boîte.</p></div>
          <div className={isOperational ? 'automation-status active' : mailbox.requiresReconnect ? 'automation-status reconnect' : 'automation-status'}><span aria-hidden="true" />{isOperational ? 'Surveillance active' : mailbox.requiresReconnect ? 'Reconnexion nécessaire' : 'Connexion requise'}</div>
        </div>
        <div className="manual-sync-row">
          <div><strong>Vérification immédiate</strong><p>Utile pour tester sans attendre le prochain passage automatique.</p></div>
          <div className="sync-controls">
            <label htmlFor="mailbox-max-results">Maximum</label>
            <select id="mailbox-max-results" value={maxResults} disabled={!isOperational || busy} onChange={(event) => setMaxResults(Number(event.target.value))}>
              {[1, 3, 5, 10, 20].map((value) => <option key={value} value={value}>{value} email{value > 1 ? 's' : ''}</option>)}
            </select>
            <button className="button primary" type="button" disabled={!isOperational || busy} onClick={() => onSync(maxResults)}>{busy ? 'Traitement…' : 'Vérifier maintenant →'}</button>
          </div>
        </div>
        <div className="privacy-note"><span aria-hidden="true">◆</span><p><strong>Confidentialité :</strong> le corps sert uniquement à la décision en mémoire. Il n’est jamais conservé en base.</p></div>
        {syncResult && (
          <div className="sync-result" aria-live="polite">
            <div className="sync-metrics"><article><strong>{syncResult.processedCount}</strong><span>traités</span></article><article><strong>{syncResult.classifiedCount}</strong><span>classés</span></article><article><strong>{syncResult.destinationAppliedCount}</strong><span>destinations appliquées</span></article><article><strong>{syncResult.failureCount}</strong><span>erreurs</span></article></div>
            {syncResult.processedCount === 0 ? <p className="empty-sync">Aucun nouvel email à traiter parmi les {syncResult.discoveredCount} détectés.</p> : (
              <div className="sync-result-list">{syncResult.results.map((item) => (
                <article key={item.externalMessageId}><span className={item.error ? 'result-dot error' : item.isClassified ? 'result-dot classified' : 'result-dot'} /><div><strong>{item.subject ?? 'Sujet indisponible'}</strong><small>{item.error ?? (item.matchedCriteria.join(' · ') || item.noMatchReason)}</small></div>{item.destinationApplied && <span className="status-pill active">{destinationName} appliqué{isGmail ? '' : 'e'}</span>}</article>
              ))}</div>
            )}
          </div>
        )}
      </section>
    </div>
  )
}
