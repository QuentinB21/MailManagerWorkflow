import { useEffect, useState, type ReactNode } from 'react'
import { appPath } from '../appPaths'
import { api } from '../api'
import { useAuth } from '../auth'
import { legalPaths } from '../legal'
import type { LegalStatus } from '../types'

export function LegalAcceptanceBoundary({ children }: { children: ReactNode }) {
  const auth = useAuth()
  const [status, setStatus] = useState<LegalStatus>()
  const [accepted, setAccepted] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const load = () => {
    setError('')
    api.legalStatus().then(setStatus).catch((reason: Error) => setError(reason.message))
  }

  useEffect(load, [])

  if (auth.isDemo) return children
  if (error) return <main className="legal-gate"><section className="legal-gate-card"><img src={appPath('logo.svg')} alt="" /><p className="overline">Accès interrompu</p><h1>Impossible de vérifier les conditions.</h1><p>{error}</p><div className="legal-gate-actions"><button className="button primary" type="button" onClick={load}>Réessayer</button><button className="button text" type="button" onClick={() => void auth.logout()}>Se déconnecter</button></div></section></main>
  if (!status) return <main className="auth-loading" aria-live="polite"><img src={appPath('logo.svg')} alt="" /><span>Vérification de votre espace…</span></main>
  if (status.isAccepted) return children

  const submit = async () => {
    if (!accepted) return
    setBusy(true); setError('')
    try { setStatus(await api.acceptLegalDocuments()) }
    catch (reason) { setError((reason as Error).message) }
    finally { setBusy(false) }
  }

  return <main className="legal-gate">
    <section className="legal-gate-card" aria-labelledby="legal-gate-title">
      <img src={appPath('logo.svg')} alt="" />
      <p className="overline">Avant de commencer</p>
      <h1 id="legal-gate-title">Un cadre clair pour vos données.</h1>
      <p>Mail Manager traite la configuration de vos boîtes et lit transitoirement les emails nécessaires au classement. Prenez connaissance des documents applicables avant d’utiliser le service.</p>
      <div className="legal-gate-documents">
        <a href={legalPaths.terms} target="_blank" rel="noreferrer"><span>Conditions d’utilisation</span><small>Version {status.termsVersion}</small><strong>↗</strong></a>
        <a href={legalPaths.privacy} target="_blank" rel="noreferrer"><span>Politique de confidentialité</span><small>Version {status.privacyVersion}</small><strong>↗</strong></a>
      </div>
      <label className="legal-agreement"><input type="checkbox" checked={accepted} onChange={(event) => setAccepted(event.target.checked)} /><span>J’accepte les conditions d’utilisation et je reconnais avoir pris connaissance de la politique de confidentialité. Cette reconnaissance ne constitue pas un consentement à des usages facultatifs.</span></label>
      <div className="legal-gate-actions"><button className="button primary" type="button" disabled={!accepted || busy} onClick={() => void submit()}>{busy ? 'Enregistrement…' : 'Continuer vers Mail Manager →'}</button><button className="button text" type="button" onClick={() => void auth.logout()}>Se déconnecter</button></div>
    </section>
  </main>
}
