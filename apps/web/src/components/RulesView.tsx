import type { FormEvent } from 'react'
import type { Label, MatchMode, Rule } from '../types'
import { EditorModal } from './EditorModal'

export type RuleFormState = {
  name: string
  destinationLabelId: string
  priority: number
  isActive: boolean
  matchMode: MatchMode
  senderAddresses: string
  senderDomains: string
  subjectKeywords: string
  bodyKeywords: string
}

type Props = {
  rules: Rule[]
  labels: Label[]
  form: RuleFormState
  editorOpen: boolean
  editingId?: string
  pendingDeleteId?: string
  busy: boolean
  onFormChange: (form: RuleFormState) => void
  onCreate: () => void
  onSubmit: (event: FormEvent) => void
  onEdit: (rule: Rule) => void
  onCancelEdit: () => void
  onToggle: (rule: Rule) => void
  onRequestDelete: (id?: string) => void
  onDelete: (rule: Rule) => void
}

const criteriaFor = (rule: Rule) => [
  ...rule.senderAddresses.map((value) => `De · ${value}`),
  ...rule.senderDomains.map((value) => `Domaine · ${value}`),
  ...rule.subjectKeywords.map((value) => `Sujet · ${value}`),
  ...rule.bodyKeywords.map((value) => `Corps · ${value}`),
]

export function RulesView({ rules, labels, form, editorOpen, editingId, pendingDeleteId, busy, onFormChange, onCreate, onSubmit, onEdit, onCancelEdit, onToggle, onRequestDelete, onDelete }: Props) {
  return (
    <>
      <section className="surface resource-list">
        <div className="section-header"><div><p className="overline">Moteur</p><h2>Règles de classement</h2><p>La plus petite priorité est évaluée en premier.</p></div><div className="section-actions"><span className="count-badge">{rules.length}</span><button className="button primary" type="button" disabled={labels.length === 0} title={labels.length === 0 ? 'Créez d’abord une destination.' : undefined} onClick={onCreate}>Ajouter une règle</button></div></div>
        {rules.length === 0 ? (
          <div className="empty-state"><span className="empty-icon">R</span><h3>Aucune règle</h3><p>{labels.length === 0 ? 'Créez d’abord une destination, puis définissez les emails qui doivent y être classés.' : 'Ajoutez au moins un critère pour commencer le classement.'}</p>{labels.length > 0 && <button className="button primary" type="button" onClick={onCreate}>Ajouter une règle</button>}</div>
        ) : (
          <div className="rule-stack">
            {rules.map((rule) => (
              <article className={editingId === rule.id ? 'rule-item selected' : 'rule-item'} key={rule.id}>
                <div className="rule-topline"><span className="priority-badge"><small>Priorité</small>{rule.priority}</span><span className={rule.isActive ? 'status success' : 'status neutral'}>{rule.isActive ? 'Active' : 'Inactive'}</span><span className="mode-badge">{rule.matchMode === 'Any' ? 'Au moins un critère' : 'Tous les critères'}</span></div>
                <h3>{rule.name}</h3><p className="destination">Vers <strong>{rule.destinationLabelName}</strong></p>
                <div className="criteria-list">{criteriaFor(rule).map((criterion) => <span key={criterion}>{criterion}</span>)}</div>
                {pendingDeleteId === rule.id ? (
                  <div className="inline-confirm"><span>Supprimer cette règle ?</span><button className="button danger small" onClick={() => onDelete(rule)} disabled={busy}>Supprimer</button><button className="button ghost small" onClick={() => onRequestDelete()}>Annuler</button></div>
                ) : (
                  <div className="resource-actions"><button className="text-action" onClick={() => onEdit(rule)}>Modifier</button><button className="text-action" onClick={() => onToggle(rule)} disabled={busy}>{rule.isActive ? 'Désactiver' : 'Activer'}</button><button className="text-action danger-text" onClick={() => onRequestDelete(rule.id)} disabled={busy}>Supprimer</button></div>
                )}
              </article>
            ))}
          </div>
        )}
      </section>

      {editorOpen && (
        <EditorModal wide eyebrow={editingId ? 'Modification' : 'Nouvelle règle'} title={editingId ? 'Modifier la règle' : 'Ajouter une règle'} onClose={onCancelEdit}>
          <form className="stack-form modal-form" onSubmit={onSubmit}>
            <label>Nom de la règle<input autoFocus required value={form.name} onChange={(event) => onFormChange({ ...form, name: event.target.value })} placeholder="Ex. Emails du client Acme" /></label>
            <div className="field-grid three">
              <label>Destination<select required value={form.destinationLabelId} onChange={(event) => onFormChange({ ...form, destinationLabelId: event.target.value })}><option value="">Choisir…</option>{labels.map((label) => <option key={label.id} value={label.id}>{label.name}{label.isActive ? '' : ' (inactive)'}</option>)}</select></label>
              <label>Priorité<input type="number" min="0" value={form.priority} onChange={(event) => onFormChange({ ...form, priority: Number(event.target.value) })} /></label>
              <label>Mode<select value={form.matchMode} onChange={(event) => onFormChange({ ...form, matchMode: event.target.value as MatchMode })}><option value="Any">Au moins un</option><option value="All">Tous</option></select></label>
            </div>
            <div className="criteria-fields"><p>Critères <small>Séparez plusieurs valeurs par des virgules.</small></p>
              <label>Adresses expéditeur<input value={form.senderAddresses} onChange={(event) => onFormChange({ ...form, senderAddresses: event.target.value })} placeholder="alice@client.fr" /></label>
              <label>Domaines expéditeur<input value={form.senderDomains} onChange={(event) => onFormChange({ ...form, senderDomains: event.target.value })} placeholder="client.fr" /></label>
              <label>Mots-clés du sujet<input value={form.subjectKeywords} onChange={(event) => onFormChange({ ...form, subjectKeywords: event.target.value })} placeholder="projet alpha, devis" /></label>
              <label>Mots-clés du corps<input value={form.bodyKeywords} onChange={(event) => onFormChange({ ...form, bodyKeywords: event.target.value })} /></label>
            </div>
            <label className="switch-row"><span><strong>Règle active</strong><small>Une règle inactive est ignorée par le moteur.</small></span><input type="checkbox" checked={form.isActive} onChange={(event) => onFormChange({ ...form, isActive: event.target.checked })} /></label>
            <div className="modal-actions"><button className="button ghost" type="button" onClick={onCancelEdit}>Annuler</button><button className="button primary" disabled={busy || labels.length === 0}>{busy ? 'Enregistrement…' : editingId ? 'Enregistrer les modifications' : 'Créer la règle'}</button></div>
          </form>
        </EditorModal>
      )}
    </>
  )
}
