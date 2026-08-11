import type { FormEvent } from 'react'
import type { Label, MatchMode, Rule } from '../types'

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
  editingId?: string
  pendingDeleteId?: string
  busy: boolean
  onFormChange: (form: RuleFormState) => void
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

export function RulesView({ rules, labels, form, editingId, pendingDeleteId, busy, onFormChange, onSubmit, onEdit, onCancelEdit, onToggle, onRequestDelete, onDelete }: Props) {
  return (
    <div className="configuration-layout rules-layout">
      <section className="surface resource-list">
        <div className="section-header"><div><p className="overline">Moteur</p><h2>Règles de classement</h2><p>La plus petite priorité est évaluée en premier.</p></div><span className="count-badge">{rules.length}</span></div>
        {rules.length === 0 ? (
          <div className="empty-state"><span className="empty-icon">R</span><h3>Aucune règle</h3><p>Ajoutez au moins un critère pour commencer.</p></div>
        ) : (
          <div className="rule-stack">
            {rules.map((rule) => (
              <article className={editingId === rule.id ? 'rule-item selected' : 'rule-item'} key={rule.id}>
                <div className="rule-topline"><span className="priority-badge">P{rule.priority}</span><span className={rule.isActive ? 'status success' : 'status neutral'}>{rule.isActive ? 'Active' : 'Inactive'}</span><span className="mode-badge">{rule.matchMode}</span></div>
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

      <aside className="surface editor-panel rule-editor">
        <div className="editor-heading"><div><p className="overline">{editingId ? 'Modification' : 'Nouvelle'}</p><h2>{editingId ? 'Modifier la règle' : 'Ajouter une règle'}</h2></div>{editingId && <button className="button ghost small" onClick={onCancelEdit}>Annuler</button>}</div>
        <form className="stack-form" onSubmit={onSubmit}>
          <label>Nom de la règle<input required value={form.name} onChange={(event) => onFormChange({ ...form, name: event.target.value })} placeholder="Ex. Emails du client Acme" /></label>
          <div className="field-grid three">
            <label>Label<select required value={form.destinationLabelId} onChange={(event) => onFormChange({ ...form, destinationLabelId: event.target.value })}><option value="">Choisir…</option>{labels.map((label) => <option key={label.id} value={label.id}>{label.name}{label.isActive ? '' : ' (inactif)'}</option>)}</select></label>
            <label>Priorité<input type="number" min="0" value={form.priority} onChange={(event) => onFormChange({ ...form, priority: Number(event.target.value) })} /></label>
            <label>Mode<select value={form.matchMode} onChange={(event) => onFormChange({ ...form, matchMode: event.target.value as MatchMode })}><option value="Any">Any</option><option value="All">All</option></select></label>
          </div>
          <div className="criteria-fields"><p>Critères <small>Séparez plusieurs valeurs par des virgules.</small></p>
            <label>Adresses expéditeur<input value={form.senderAddresses} onChange={(event) => onFormChange({ ...form, senderAddresses: event.target.value })} placeholder="alice@client.fr" /></label>
            <label>Domaines expéditeur<input value={form.senderDomains} onChange={(event) => onFormChange({ ...form, senderDomains: event.target.value })} placeholder="client.fr" /></label>
            <label>Mots-clés du sujet<input value={form.subjectKeywords} onChange={(event) => onFormChange({ ...form, subjectKeywords: event.target.value })} placeholder="projet alpha, devis" /></label>
            <label>Mots-clés du corps<input value={form.bodyKeywords} onChange={(event) => onFormChange({ ...form, bodyKeywords: event.target.value })} /></label>
          </div>
          <label className="switch-row"><span><strong>Règle active</strong><small>Une règle inactive est ignorée par le moteur.</small></span><input type="checkbox" checked={form.isActive} onChange={(event) => onFormChange({ ...form, isActive: event.target.checked })} /></label>
          <button className="button primary full" disabled={busy || labels.length === 0}>{busy ? 'Enregistrement…' : editingId ? 'Enregistrer les modifications' : 'Créer la règle'}</button>
        </form>
      </aside>
    </div>
  )
}
