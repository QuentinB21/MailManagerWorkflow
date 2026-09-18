import { useEffect, useState } from 'react'
import { api } from '../api'

export function AssistantConnectionPanel() {
  const [connection, setConnection] = useState<Awaited<ReturnType<typeof api.mcpConnection>>>()
  const [message, setMessage] = useState('')
  useEffect(() => { api.mcpConnection().then(setConnection).catch(() => setMessage('Les informations de connexion sont indisponibles. Réessayez en rechargeant la page.')) }, [])

  async function copyUrl() {
    if (!connection) return
    try { await navigator.clipboard.writeText(connection.url); setMessage('Adresse copiée.') }
    catch { setMessage('Copie indisponible. Sélectionnez et copiez l’adresse ci-dessous.') }
  }

  return <div className="page account-privacy-page">
    <section className="surface account-privacy-panel">
      <div className="section-heading"><div><p className="overline">Assistant IA</p><h2>Configurez votre classement avec ChatGPT ou Claude</h2><p>Connectez votre assistant à MailManager, décrivez votre besoin et confirmez les changements dans la conversation. Votre assistant utilise ses propres ressources IA.</p></div></div>
      {connection && <>
        <label>Adresse du connecteur<input readOnly value={connection.url} aria-label="Adresse du connecteur MCP" onFocus={(event) => event.target.select()} /></label>
        <button type="button" className="button secondary" onClick={() => void copyUrl()}>Copier l’adresse</button>
        <div className="account-privacy-actions">
          <article><div><strong>ChatGPT</strong><p>Ajoutez un connecteur MCP personnalisé dans les paramètres des applications, avec cette adresse et l’authentification OAuth. Si demandé, utilisez l’identifiant <code>{connection.chatGptClientId}</code>, sans secret. La disponibilité dépend des options de votre compte ChatGPT.</p></div></article>
          <article><div><strong>Claude</strong><p>Ajoutez un connecteur personnalisé dans les paramètres des connecteurs. Dans les options avancées, utilisez l’identifiant <code>{connection.claudeClientId}</code>, sans secret, puis connectez-vous à MailManager.</p></div></article>
        </div>
        <p>Exemple : « Crée une destination Comptabilité et une règle pour les messages de compta@fournisseur.fr dont le sujet contient facture. Montre-moi les changements avant de les appliquer. »</p>
        <p>Votre assistant peut consulter vos boîtes, règles et destinations, mais ne reçoit pas le contenu de vos emails. Il peut tester un exemple que vous lui fournissez. La confirmation dépend de votre assistant et de ses réglages. Pour retirer son accès, révoquez l’autorisation correspondante dans votre espace d’identité MailManager.</p>
        {connection.url.startsWith('http://') && <p>Cette adresse locale permet les tests de développement. ChatGPT et Claude nécessitent un serveur accessible depuis leurs services ; utilisez l’adresse HTTPS de MailManager déployé.</p>}
      </>}
      {message && <p role="status">{message}</p>}
    </section>
  </div>
}
