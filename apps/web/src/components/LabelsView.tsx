import type { FormEvent } from 'react'
import type { Label, Rule } from '../types'
import { EditorModal } from './EditorModal'

export type LabelFormState = {
  name: string
  color: string
  isActive: boolean
}

type Props = {
  labels: Label[]
  rules: Rule[]
  form: LabelFormState
  editorOpen: boolean
  editingId?: string
  pendingDeleteId?: string
  busy: boolean
  readOnly?: boolean
  onFormChange: (form: LabelFormState) => void
  onCreate: () => void
  onSubmit: (event: FormEvent) => void
  onEdit: (label: Label) => void
  onCancelEdit: () => void
  onToggle: (label: Label) => void
  onRequestDelete: (id?: string) => void
  onDelete: (label: Label) => void
}

export function LabelsView({ labels, rules, form, editorOpen, editingId, pendingDeleteId, busy, readOnly = false, onFormChange, onCreate, onSubmit, onEdit, onCancelEdit, onToggle, onRequestDelete, onDelete }: Props) {
  return (
    <>
      <section className="surface resource-list">
        <div className="section-header">
          <div><p className="overline">Destinations</p><h2>Libellés Gmail</h2><p>Organisez les projets ou clients utilisés par vos règles.</p></div>
          <div className="section-actions"><span className="count-badge">{labels.length}</span><button className="button primary" type="button" disabled={readOnly} onClick={onCreate}>Ajouter une destination</button></div>
        </div>

        {labels.length === 0 ? (
          <div className="empty-state"><span className="empty-icon">L</span><h3>Aucune destination</h3><p>Créez votre premier libellé Gmail pour pouvoir lui associer une règle.</p><button className="button primary" type="button" disabled={readOnly} onClick={onCreate}>Ajouter une destination</button></div>
        ) : (
          <div className="resource-cards">
            {labels.map((label) => {
              const usageCount = rules.filter((rule) => rule.destinationLabelId === label.id).length
              const isConfirming = pendingDeleteId === label.id
              return (
                <article className={editingId === label.id ? 'resource-card selected' : 'resource-card'} key={label.id}>
                  <span className="label-swatch" style={{ backgroundColor: label.color || '#64748b' }} />
                  <div className="resource-main">
                    <div className="resource-title"><h3>{label.name}</h3><span className={label.isActive ? 'status success' : 'status neutral'}>{label.isActive ? 'Actif' : 'Inactif'}</span></div>
                    <p>{usageCount ? `Utilisé par ${usageCount} règle${usageCount > 1 ? 's' : ''}` : 'Aucune règle associée'}</p>
                    <p className={label.externalLabelId ? 'provider-state synced' : 'provider-state pending'}>
                      <span aria-hidden="true">{label.externalLabelId ? '✓' : '↗'}</span>
                      {label.externalLabelId ? 'Créé dans Gmail' : 'Sera créé automatiquement au premier classement'}
                    </p>
                    {isConfirming ? (
                      <div className="inline-confirm"><span>Supprimer définitivement ?</span><button className="button danger small" onClick={() => onDelete(label)} disabled={busy}>Supprimer</button><button className="button ghost small" onClick={() => onRequestDelete()}>Annuler</button></div>
                    ) : (
                      <div className="resource-actions">
                        <button className="text-action" disabled={readOnly} onClick={() => onEdit(label)}>Modifier</button>
                        <button className="text-action" onClick={() => onToggle(label)} disabled={busy || readOnly}>{label.isActive ? 'Désactiver' : 'Activer'}</button>
                        <button className="text-action danger-text" disabled={usageCount > 0 || busy || readOnly} title={usageCount ? 'Supprimez ou modifiez les règles associées avant ce label.' : undefined} onClick={() => onRequestDelete(label.id)}>Supprimer</button>
                      </div>
                    )}
                  </div>
                </article>
              )
            })}
          </div>
        )}
      </section>

      {editorOpen && (
        <EditorModal eyebrow={editingId ? 'Modification' : 'Nouvelle destination'} title={editingId ? 'Modifier la destination' : 'Ajouter une destination'} onClose={onCancelEdit}>
          <form className="stack-form modal-form" onSubmit={onSubmit}>
            <label>Nom du libellé Gmail<input autoFocus required maxLength={150} value={form.name} onChange={(event) => onFormChange({ ...form, name: event.target.value })} placeholder="Ex. Client Acme" /></label>
            <label>Couleur<span className="color-input"><input type="color" value={form.color} onChange={(event) => onFormChange({ ...form, color: event.target.value })} /><input value={form.color} pattern="#[0-9a-fA-F]{6}" onChange={(event) => onFormChange({ ...form, color: event.target.value })} aria-label="Code couleur" /></span><small>Gmail et Outlook utilisent des palettes prédéfinies : la couleur disponible la plus proche sera appliquée.</small></label>
            <label className="switch-row"><span><strong>Destination active</strong><small>Les règles vers une destination inactive ne classent aucun email.</small></span><input type="checkbox" checked={form.isActive} onChange={(event) => onFormChange({ ...form, isActive: event.target.checked })} /></label>
            <div className="modal-actions"><button className="button ghost" type="button" onClick={onCancelEdit}>Annuler</button><button className="button primary" disabled={busy}>{busy ? 'Enregistrement…' : editingId ? 'Enregistrer les modifications' : 'Créer la destination'}</button></div>
          </form>
          <div className="editor-tip"><strong>Bon à savoir</strong><p>Une destination utilisée par une règle ne peut pas être supprimée. Modifiez d’abord la règle concernée.</p></div>
        </EditorModal>
      )}
    </>
  )
}
