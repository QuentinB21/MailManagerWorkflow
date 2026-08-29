import { useState } from 'react'
import { legalPaths } from '../legal'

type Props = {
  busy: boolean
  onExport: () => void
  onDeleteData: () => void
  onManageIdentity: () => void
}

export function AccountPrivacyPanel({ busy, onExport, onDeleteData, onManageIdentity }: Props) {
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [confirmation, setConfirmation] = useState('')
  const canDelete = confirmation === 'SUPPRIMER'

  return <div className="page account-privacy-page">
    <section className="surface account-privacy-panel">
      <div className="section-heading"><div><p className="overline">Données personnelles</p><h2>Contrôlez les données de votre espace</h2><p>Téléchargez une copie portable ou effacez les boîtes, règles, destinations et historiques rattachés à votre compte.</p></div></div>
      <div className="account-privacy-actions">
        <article><div><strong>Exporter mes données</strong><p>Fichier JSON lisible par machine. Les secrets et jetons OAuth sont exclus.</p></div><button className="button secondary" type="button" disabled={busy} onClick={onExport}>Télécharger l’export</button></article>
        <article><div><strong>Identité et mot de passe</strong><p>Prénom, nom, courriel et identifiants sont administrés dans l’espace sécurisé Keycloak.</p></div><button className="button secondary" type="button" disabled={busy} onClick={onManageIdentity}>Gérer mon identité</button></article>
        <article className="danger-zone"><div><strong>Effacer mes données Mail Manager</strong><p>Révoque l’accès Gmail lorsque possible, puis supprime définitivement toutes les données applicatives. Le profil d’authentification Keycloak reste géré séparément.</p></div>{!deleteOpen ? <button className="button text danger-text" type="button" disabled={busy} onClick={() => setDeleteOpen(true)}>Préparer la suppression</button> : <div className="account-delete-confirm"><label>Saisissez <strong>SUPPRIMER</strong><input value={confirmation} onChange={(event) => setConfirmation(event.target.value)} autoComplete="off" /></label><div><button className="button danger" type="button" disabled={busy || !canDelete} onClick={onDeleteData}>Effacer définitivement</button><button className="button text" type="button" onClick={() => { setDeleteOpen(false); setConfirmation('') }}>Annuler</button></div></div>}</article>
      </div>
      <p className="account-privacy-links">Consultez la <a href={legalPaths.privacy}>politique de confidentialité</a> ou contactez le responsable du traitement pour exercer un autre droit.</p>
    </section>
  </div>
}
