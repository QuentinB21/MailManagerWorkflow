import type { FormEvent } from 'react'
import type { ClassificationResult, WorkflowResult } from '../types'

export type EmailFormState = {
  externalMessageId: string
  sender: string
  subject: string
  body: string
}

type Props = {
  form: EmailFormState
  result?: ClassificationResult
  resultSource?: 'simulation' | 'workflow'
  busy: boolean
  onFormChange: (form: EmailFormState) => void
  onGenerateId: () => void
  onSimulate: (event: FormEvent) => void
  onRunWorkflow: () => void
  embedded?: boolean
}

export function WorkflowTestView({ form, result, resultSource, busy, onFormChange, onGenerateId, onSimulate, onRunWorkflow, embedded = false }: Props) {
  return (
    <div className={embedded ? 'classification-test' : 'page'}>
      {!embedded && <div className="page-header">
        <div><p className="overline">Banc de test</p><h1>Tester le classement</h1><p>Envoyez un email fictif et observez précisément la décision du moteur.</p></div>
      </div>}

      <div className="test-layout">
        <section className="surface test-form-card">
          <div className="section-header"><div><h2>Email normalisé</h2><p>Ces données reproduisent le format transmis par un fournisseur.</p></div></div>
          <form className="stack-form" onSubmit={onSimulate}>
            <label>Identifiant externe<span className="input-with-action"><input required value={form.externalMessageId} onChange={(event) => onFormChange({ ...form, externalMessageId: event.target.value })} /><button type="button" className="button ghost small" onClick={onGenerateId}>Nouveau</button></span><small>Conservez-le pour tester l’idempotence, changez-le pour un nouveau traitement.</small></label>
            <label>Expéditeur<input required type="email" value={form.sender} onChange={(event) => onFormChange({ ...form, sender: event.target.value })} placeholder="contact@client.fr" /></label>
            <label>Sujet<input value={form.subject} onChange={(event) => onFormChange({ ...form, subject: event.target.value })} /></label>
            <label>Corps<textarea rows={7} value={form.body} onChange={(event) => onFormChange({ ...form, body: event.target.value })} /></label>
            <div className="test-actions"><button type="submit" className="button secondary" disabled={busy}>{busy ? 'Traitement…' : 'Simuler via l’API'}</button><button type="button" className="button primary" disabled={busy} onClick={onRunWorkflow}>{busy ? 'Traitement…' : 'Exécuter via n8n'} <span>→</span></button></div>
            <div className="action-help"><span><i className="help-dot simulation" />Simulation : aucune écriture</span><span><i className="help-dot workflow" />n8n : résultat historisé</span></div>
          </form>
        </section>

        <section className="surface result-card" aria-live="polite">
          {!result ? (
            <div className="result-placeholder"><span className="result-placeholder-icon">⌁</span><h2>Prêt pour un test</h2><p>Le résultat expliquera le label choisi, la règle gagnante et chaque critère correspondant.</p><div className="mini-flow"><span>Email</span><i>→</i><span>Règles</span><i>→</i><span>Résultat</span></div></div>
          ) : (
            <div className="result-content">
              <div className="result-header"><span className={result.isClassified ? 'decision-icon success' : 'decision-icon neutral'}>{result.isClassified ? '✓' : '—'}</span><div><p className="overline">{resultSource === 'workflow' ? `Workflow n8n · ${(result as WorkflowResult).workflowOutcome}` : 'Simulation API'}</p><h2>{result.isClassified ? 'Email classé' : 'Aucune correspondance'}</h2></div></div>
              {result.isClassified ? (
                <div className="decision-summary"><small>Label de destination</small><strong>{result.label?.name}</strong></div>
              ) : <div className="decision-summary neutral"><small>Décision</small><strong>Email laissé non classé</strong></div>}
              {result.matchedRule && <dl className="result-details"><div><dt>Règle gagnante</dt><dd>{result.matchedRule.name}</dd></div><div><dt>Priorité</dt><dd>{result.matchedRule.priority}</dd></div></dl>}
              {result.matchedCriteria.length > 0 && <div className="matched-block"><h3>Critères correspondants</h3>{result.matchedCriteria.map((criterion) => <span key={criterion}>✓ {criterion}</span>)}</div>}
              {result.noMatchReason && <p className="no-match-reason">{result.noMatchReason}</p>}
              {result.wasAlreadyProcessed && <div className="notice-card warning"><strong>Traitement déjà effectué</strong><p>Le résultat existant a été réutilisé sans créer de doublon.</p></div>}
              <div className="result-footer"><span className={resultSource === 'workflow' ? 'status success' : 'status neutral'}>{resultSource === 'workflow' ? 'Enregistré dans l’historique' : 'Non persisté'}</span>{result.processingLogId && <code>{result.processingLogId}</code>}</div>
            </div>
          )}
        </section>
      </div>
    </div>
  )
}
