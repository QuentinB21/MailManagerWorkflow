import { useEffect, useState, type CSSProperties, type FormEvent } from 'react'
import type { Label, MatchMode, Rule } from '../types'
import { EditorModal } from './EditorModal'

export type RuleConditionType = 'senderAddress' | 'senderDomain' | 'subjectKeyword' | 'bodyKeyword'

export type RuleCondition = {
  id: string
  type: RuleConditionType
  value: string
}

export type RuleFormState = {
  name: string
  destinationLabelId: string
  priority: number
  isActive: boolean
  matchMode: MatchMode
  conditions: RuleCondition[]
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
  ...rule.senderAddresses.map((value) => ({ field: 'Expéditeur', comparison: 'est', value })),
  ...rule.senderDomains.map((value) => ({ field: 'Expéditeur', comparison: 'se termine par', value: `@${value.replace(/^@/, '')}` })),
  ...rule.subjectKeywords.map((value) => ({ field: 'Sujet', comparison: 'contient', value })),
  ...rule.bodyKeywords.map((value) => ({ field: 'Message', comparison: 'contient', value })),
]

const conditionOptions: Array<{
  type: RuleConditionType
  marker: string
  title: string
  description: string
  label: string
  placeholder: string
}> = [
  { type: 'senderAddress', marker: '@', title: 'Adresse exacte', description: 'Un expéditeur précis', label: "L’adresse de l’expéditeur est", placeholder: 'alice@client.fr' },
  { type: 'senderDomain', marker: '*', title: 'Domaine expéditeur', description: 'Toutes les adresses d’un domaine', label: "L’adresse de l’expéditeur se termine par", placeholder: 'client.fr' },
  { type: 'subjectKeyword', marker: 'A', title: 'Mot-clé dans le sujet', description: 'Le titre du mail contient ce texte', label: 'Le sujet contient', placeholder: 'Projet Alpha' },
  { type: 'bodyKeyword', marker: '¶', title: 'Mot-clé dans le message', description: 'Le contenu du mail contient ce texte', label: 'Le message contient', placeholder: 'demande de devis' },
]

let nextConditionId = 0
export const createRuleCondition = (type: RuleConditionType, value = ''): RuleCondition => ({
  id: `condition-${Date.now()}-${nextConditionId++}`,
  type,
  value,
})

export function RulesView({ rules, labels, form, editorOpen, editingId, pendingDeleteId, busy, onFormChange, onCreate, onSubmit, onEdit, onCancelEdit, onToggle, onRequestDelete, onDelete }: Props) {
  const [conditionPickerOpen, setConditionPickerOpen] = useState(false)
  const [expandedRuleIds, setExpandedRuleIds] = useState<string[]>([])

  useEffect(() => {
    if (!editorOpen) setConditionPickerOpen(false)
  }, [editorOpen])

  function addCondition(type: RuleConditionType) {
    onFormChange({ ...form, conditions: [...form.conditions, createRuleCondition(type)] })
    setConditionPickerOpen(false)
  }

  function updateCondition(id: string, value: string) {
    onFormChange({ ...form, conditions: form.conditions.map((condition) => condition.id === id ? { ...condition, value } : condition) })
  }

  function removeCondition(id: string) {
    onFormChange({ ...form, conditions: form.conditions.filter((condition) => condition.id !== id) })
  }

  function toggleRuleDetails(id: string) {
    setExpandedRuleIds((ids) => ids.includes(id) ? ids.filter((item) => item !== id) : [...ids, id])
  }

  const hasIncompleteCondition = form.conditions.some((condition) => !condition.value.trim())

  return (
    <>
      <section className="surface resource-list">
        <div className="section-header"><div><p className="overline">Moteur</p><h2>Règles de classement</h2><p>La plus petite priorité est évaluée en premier.</p></div><div className="section-actions"><span className="count-badge">{rules.length}</span><button className="button primary" type="button" disabled={labels.length === 0} title={labels.length === 0 ? 'Créez d’abord une destination.' : undefined} onClick={onCreate}>Ajouter une règle</button></div></div>
        {rules.length === 0 ? (
          <div className="empty-state"><span className="empty-icon">R</span><h3>Aucune règle</h3><p>{labels.length === 0 ? 'Créez d’abord une destination, puis définissez les emails qui doivent y être classés.' : 'Ajoutez au moins un critère pour commencer le classement.'}</p>{labels.length > 0 && <button className="button primary" type="button" onClick={onCreate}>Ajouter une règle</button>}</div>
        ) : (
          <div className="rule-grid">
            {rules.map((rule) => {
              const criteria = criteriaFor(rule)
              const destination = labels.find((label) => label.id === rule.destinationLabelId)
              const isExpanded = expandedRuleIds.includes(rule.id)
              const detailsId = `rule-details-${rule.id}`
              return (
                <article className={`rule-card${editingId === rule.id ? ' selected' : ''}${rule.isActive ? '' : ' inactive'}`} key={rule.id}>
                  <div className="rule-summary">
                    <div className="rule-summary-identity"><h3>{rule.name}</h3><span className={rule.isActive ? 'status success' : 'status neutral'}>{rule.isActive ? 'Active' : 'Inactive'}</span></div>
                    <span className="rule-priority"><small>Priorité</small><strong>{rule.priority}</strong></span>
                    {pendingDeleteId === rule.id ? (
                      <div className="inline-confirm rule-summary-confirm"><span>Supprimer cette règle ?</span><button className="button danger small" onClick={() => onDelete(rule)} disabled={busy}>Supprimer</button><button className="button ghost small" onClick={() => onRequestDelete()}>Annuler</button></div>
                    ) : (
                      <div className="resource-actions"><button className="text-action" onClick={() => onEdit(rule)}>Modifier</button><button className="text-action" onClick={() => onToggle(rule)} disabled={busy}>{rule.isActive ? 'Désactiver' : 'Activer'}</button><button className="text-action danger-text" onClick={() => onRequestDelete(rule.id)} disabled={busy}>Supprimer</button></div>
                    )}
                    <button className={`rule-expand${isExpanded ? ' expanded' : ''}`} type="button" aria-expanded={isExpanded} aria-controls={detailsId} aria-label={`${isExpanded ? 'Masquer' : 'Afficher'} les détails de la règle « ${rule.name} »`} onClick={() => toggleRuleDetails(rule.id)}>
                      <svg viewBox="0 0 20 20" aria-hidden="true"><path d="m5 7.5 5 5 5-5" /></svg>
                    </button>
                  </div>

                  {isExpanded && (
                    <div className="rule-decision" id={detailsId}>
                      <div className="rule-when-heading">
                        <span className="decision-marker">Si</span>
                        <div><strong>{rule.matchMode === 'Any' ? 'Une condition suffit' : 'Toutes les conditions'}</strong><small>{rule.matchMode === 'Any' ? 'Le premier critère reconnu déclenche la règle.' : 'Chaque critère doit être reconnu dans le même email.'}</small></div>
                      </div>
                      <div className="rule-criteria-flow">
                        {criteria.map((criterion, index) => (
                          <div className="criterion-group" key={`${criterion.field}-${criterion.value}-${index}`}>
                            {index > 0 && <span className="criterion-joiner">{rule.matchMode === 'Any' ? 'ou' : 'et'}</span>}
                            <div className="criterion-line"><span>{criterion.field}</span><small>{criterion.comparison}</small><code>{criterion.value}</code></div>
                          </div>
                        ))}
                      </div>

                      <div className="decision-connector"><span /><small>alors</small><span /></div>

                      <div className="rule-destination" style={{ '--destination-color': destination?.color ?? '#c64a2f' } as CSSProperties}>
                        <span className="destination-swatch" aria-hidden="true" /><div><small>Classer dans</small><strong>{rule.destinationLabelName}</strong></div>
                      </div>
                    </div>
                  )}
                </article>
              )
            })}
          </div>
        )}
      </section>

      {editorOpen && (
        <EditorModal wide eyebrow={editingId ? 'Modification' : 'Nouvelle règle'} title={editingId ? 'Modifier la règle' : 'Ajouter une règle'} onClose={onCancelEdit}>
          <form className="stack-form modal-form rule-builder" onSubmit={onSubmit}>
            <div className="rule-identity">
              <label>Nom de la règle<input autoFocus required value={form.name} onChange={(event) => onFormChange({ ...form, name: event.target.value })} placeholder="Ex. Emails du client Acme" /></label>
              <label>Classer vers<select required value={form.destinationLabelId} onChange={(event) => onFormChange({ ...form, destinationLabelId: event.target.value })}><option value="">Choisir une destination…</option>{labels.map((label) => <option key={label.id} value={label.id}>{label.name}{label.isActive ? '' : ' (inactive)'}</option>)}</select></label>
            </div>

            <section className="condition-builder" aria-labelledby="conditions-title">
              <div className="condition-builder-header">
                <div><span className="step-number">1</span><div><h3 id="conditions-title">Définir les conditions</h3><p>Ajoutez uniquement les critères utiles à cette règle.</p></div></div>
                <span className="condition-count">{form.conditions.length} {form.conditions.length > 1 ? 'conditions' : 'condition'}</span>
              </div>

              {form.conditions.length > 1 && (
                <div className="condition-logic">
                  <span>Déclencher si</span>
                  <select aria-label="Mode de correspondance" value={form.matchMode} onChange={(event) => onFormChange({ ...form, matchMode: event.target.value as MatchMode })}>
                    <option value="Any">au moins une</option>
                    <option value="All">toutes</option>
                  </select>
                  <span>des conditions est remplie</span>
                </div>
              )}

              {form.conditions.length === 0 ? (
                <div className="conditions-empty"><span>+</span><strong>Par quoi reconnaître ces emails ?</strong><p>Choisissez une première condition ci-dessous.</p></div>
              ) : (
                <div className="condition-list">
                  {form.conditions.map((condition, index) => {
                    const option = conditionOptions.find((item) => item.type === condition.type)!
                    return (
                      <div className="condition-row" key={condition.id}>
                        <span className="condition-index" aria-hidden="true">{index + 1}</span>
                        <label><span>{option.label}</span><input required value={condition.value} onChange={(event) => updateCondition(condition.id, event.target.value)} placeholder={option.placeholder} /></label>
                        <button className="condition-remove" type="button" onClick={() => removeCondition(condition.id)} aria-label={`Supprimer la condition « ${option.title} »`}>×</button>
                      </div>
                    )
                  })}
                </div>
              )}

              <div className="condition-add">
                <button className="add-condition-button" type="button" aria-expanded={conditionPickerOpen} onClick={() => setConditionPickerOpen((open) => !open)}><span>+</span> Ajouter une condition</button>
                {conditionPickerOpen && (
                  <div className="condition-picker">
                    {conditionOptions.map((option) => (
                      <button type="button" key={option.type} onClick={() => addCondition(option.type)}>
                        <span className="condition-marker">{option.marker}</span><span><strong>{option.title}</strong><small>{option.description}</small></span>
                      </button>
                    ))}
                  </div>
                )}
              </div>
            </section>

            <section className="rule-settings" aria-labelledby="settings-title">
              <div className="rule-settings-heading"><span className="step-number">2</span><div><h3 id="settings-title">Finaliser la règle</h3><p>La priorité départage les règles qui correspondent au même email.</p></div></div>
              <div className="rule-settings-fields">
                <label>Priorité<input type="number" min="0" value={form.priority} onChange={(event) => onFormChange({ ...form, priority: Number(event.target.value) })} /></label>
                <label className="switch-row"><span><strong>Règle active</strong><small>Elle commencera à classer les prochains emails.</small></span><input type="checkbox" checked={form.isActive} onChange={(event) => onFormChange({ ...form, isActive: event.target.checked })} /></label>
              </div>
            </section>

            <div className="modal-actions"><button className="button ghost" type="button" onClick={onCancelEdit}>Annuler</button><button className="button primary" disabled={busy || labels.length === 0 || form.conditions.length === 0 || hasIncompleteCondition}>{busy ? 'Enregistrement…' : editingId ? 'Enregistrer les modifications' : 'Créer la règle'}</button></div>
          </form>
        </EditorModal>
      )}
    </>
  )
}
