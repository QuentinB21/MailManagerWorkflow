import { useEffect, useState, type FormEvent } from 'react'
import type { GmailOAuthConfiguration, GmailSyncResult, Mailbox } from '../types'

type Props = {
  mailbox: Mailbox
  configuration: GmailOAuthConfiguration
  busy: boolean
  syncResult?: GmailSyncResult
  onSaveConfiguration: (configuration: { clientId: string; clientSecret?: string }) => void
  onDeleteConfiguration: () => void
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
  onSaveConfiguration,
  onDeleteConfiguration,
  onConnect,
  onTestConnection,
  onSync,
  onDisconnect,
}: Props) {
  const [maxResults, setMaxResults] = useState(5)
  const [confirmDisconnect, setConfirmDisconnect] = useState(false)
  const [confirmDeleteConfiguration, setConfirmDeleteConfiguration] = useState(false)
  const [showConfiguration, setShowConfiguration] = useState(!configuration.isConfigured)
  const [clientId, setClientId] = useState(configuration.clientId ?? '')
  const [clientSecret, setClientSecret] = useState('')
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    setClientId(configuration.clientId ?? '')
    setClientSecret('')
    setShowConfiguration(!configuration.isConfigured)
    setConfirmDeleteConfiguration(false)
  }, [configuration.clientId, configuration.isConfigured])

  function submitConfiguration(event: FormEvent) {
    event.preventDefault()
    onSaveConfiguration({ clientId, clientSecret: clientSecret || undefined })
  }

  async function copyRedirectUri() {
    await navigator.clipboard.writeText(configuration.redirectUri)
    setCopied(true)
    window.setTimeout(() => setCopied(false), 1800)
  }

  return (
    <div className="page mailbox-page">
      <div className="page-header">
        <div>
          <p className="overline">Fournisseur de messagerie</p>
          <h1>Boîte Gmail</h1>
          <p>Configurez Google une seule fois, connectez votre compte puis lancez un traitement réel.</p>
        </div>
        <span className={mailbox.isConnected ? 'status-pill active' : 'status-pill inactive'}>
          {mailbox.isConnected ? 'Connectée' : 'Non connectée'}
        </span>
      </div>

      {showConfiguration && !mailbox.isConnected && (
        <section className="surface oauth-setup" aria-labelledby="oauth-setup-title">
          <div className="oauth-setup-header">
            <div>
              <p className="overline">Configuration guidée</p>
              <h2 id="oauth-setup-title">Préparer la connexion Google OAuth</h2>
              <p>Google impose de créer le client OAuth dans sa console. Les liens ci-dessous ouvrent directement chaque page utile.</p>
            </div>
            {configuration.isConfigured && (
              <button className="button text" type="button" onClick={() => setShowConfiguration(false)}>Fermer</button>
            )}
          </div>

          <div className="oauth-setup-layout">
            <ol className="setup-steps">
              <li>
                <span>1</span>
                <div><strong>Activer Gmail API</strong><p>Sélectionnez ou créez votre projet Google Cloud, puis activez l’API.</p><a href={configuration.gmailApiUrl} target="_blank" rel="noreferrer">Ouvrir Gmail API ↗</a></div>
              </li>
              <li>
                <span>2</span>
                <div><strong>Configurer l’écran de consentement</strong><p>Pour ce POC, choisissez « Externe », puis ajoutez votre adresse Gmail comme utilisateur de test.</p><div className="step-links"><a href={configuration.consentScreenUrl} target="_blank" rel="noreferrer">Écran de consentement ↗</a><a href={configuration.testUsersUrl} target="_blank" rel="noreferrer">Utilisateurs de test ↗</a></div></div>
              </li>
              <li>
                <span>3</span>
                <div><strong>Créer le client OAuth</strong><p>Créez un identifiant de type <b>Application Web</b> et ajoutez exactement l’URI ci-dessous aux URI de redirection autorisées.</p><a href={configuration.oAuthClientsUrl} target="_blank" rel="noreferrer">Créer un client OAuth ↗</a></div>
              </li>
            </ol>

            <form className="oauth-form" onSubmit={submitConfiguration}>
              <div className="redirect-field">
                <label>URI de redirection autorisée</label>
                <div><code>{configuration.redirectUri}</code><button type="button" onClick={copyRedirectUri}>{copied ? 'Copiée ✓' : 'Copier'}</button></div>
              </div>
              <label>
                <span>Client ID Google</span>
                <input value={clientId} onChange={(event) => setClientId(event.target.value)} placeholder="…apps.googleusercontent.com" autoComplete="off" required />
              </label>
              <label>
                <span>Client secret Google</span>
                <input type="password" value={clientSecret} onChange={(event) => setClientSecret(event.target.value)} placeholder={configuration.hasClientSecret ? 'Laisser vide pour conserver le secret actuel' : 'Saisir le client secret'} autoComplete="new-password" required={!configuration.hasClientSecret} />
              </label>
              <div className="secret-note"><span aria-hidden="true">◆</span><p>Le secret est chiffré avant stockage et n’est jamais renvoyé à votre navigateur.</p></div>
              <button className="button primary full" type="submit" disabled={busy}>{busy ? 'Enregistrement…' : 'Enregistrer la configuration'}</button>
            </form>
          </div>
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

            {configuration.isConfigured && !mailbox.isConnected && (
              <div className="configured-banner">
                <div><span aria-hidden="true">✓</span><p><strong>Configuration OAuth prête</strong><small>{configuration.clientId}</small></p></div>
                <button className="button text" type="button" disabled={busy} onClick={() => setShowConfiguration(true)}>Modifier</button>
              </div>
            )}

            <div className="connection-actions">
              {!mailbox.isConnected ? (
                <>
                  <button className="button primary" type="button" disabled={busy || !configuration.isConfigured} onClick={onConnect}>
                    Connecter mon compte Gmail →
                  </button>
                  {configuration.isConfigured && !confirmDeleteConfiguration && (
                    <button className="button text danger-text" type="button" disabled={busy} onClick={() => setConfirmDeleteConfiguration(true)}>Supprimer la configuration</button>
                  )}
                  {confirmDeleteConfiguration && (
                    <div className="inline-confirm">
                      <span>Supprimer le Client ID et le secret enregistrés ?</span>
                      <button className="button danger" type="button" disabled={busy} onClick={onDeleteConfiguration}>Confirmer</button>
                      <button className="button text" type="button" onClick={() => setConfirmDeleteConfiguration(false)}>Annuler</button>
                    </div>
                  )}
                </>
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
