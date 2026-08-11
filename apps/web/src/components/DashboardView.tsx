import type { Label, ProcessingLog, Rule } from '../types'
import type { AppView } from './AppShell'
import { HistoryTable } from './HistoryTable'

type Props = {
  labels: Label[]
  rules: Rule[]
  logs: ProcessingLog[]
  busy: boolean
  onNavigate: (view: AppView) => void
  onRefreshHistory: () => void
}

export function DashboardView({ labels, rules, logs, busy, onNavigate, onRefreshHistory }: Props) {
  const activeLabels = labels.filter((label) => label.isActive).length
  const activeRules = rules.filter((rule) => rule.isActive).length
  const classified = logs.filter((log) => log.isClassified).length
  const classificationRate = logs.length ? Math.round((classified / logs.length) * 100) : 0

  return (
    <div className="page">
      <div className="page-header dashboard-heading">
        <div><p className="overline">Aujourd’hui</p><h1>Tableau de bord</h1><p>Configurez vos règles, testez le flux et contrôlez les dernières décisions.</p></div>
        <button className="button primary" onClick={() => onNavigate('test')}>Tester un email <span>→</span></button>
      </div>

      <section className="metric-grid" aria-label="Indicateurs">
        <article className="metric-card"><span className="metric-icon indigo">L</span><div><small>Labels actifs</small><strong>{activeLabels}</strong><p>{labels.length} configuré{labels.length > 1 ? 's' : ''}</p></div></article>
        <article className="metric-card"><span className="metric-icon cyan">R</span><div><small>Règles actives</small><strong>{activeRules}</strong><p>{rules.length} au total</p></div></article>
        <article className="metric-card"><span className="metric-icon green">✓</span><div><small>Taux de classement</small><strong>{classificationRate}%</strong><p>sur {logs.length} traitement{logs.length > 1 ? 's' : ''}</p></div></article>
      </section>

      <section className="dashboard-grid">
        <article className="surface getting-started">
          <div className="section-header"><div><p className="overline">Démarrage rapide</p><h2>Votre configuration</h2><p>Les trois éléments nécessaires au flux démontrable.</p></div></div>
          <div className="checklist">
            <button onClick={() => onNavigate('configuration')}><span className={labels.length ? 'check done' : 'check'}>{labels.length ? '✓' : '1'}</span><span><strong>Créer un label</strong><small>{labels.length ? `${labels.length} label${labels.length > 1 ? 's' : ''} disponible${labels.length > 1 ? 's' : ''}` : 'Définissez une destination'}</small></span><b>→</b></button>
            <button onClick={() => onNavigate('configuration')}><span className={activeRules ? 'check done' : 'check'}>{activeRules ? '✓' : '2'}</span><span><strong>Activer une règle</strong><small>{activeRules ? `${activeRules} règle${activeRules > 1 ? 's' : ''} prête${activeRules > 1 ? 's' : ''}` : 'Ajoutez vos critères'}</small></span><b>→</b></button>
            <button onClick={() => onNavigate('test')}><span className={logs.length ? 'check done' : 'check'}>{logs.length ? '✓' : '3'}</span><span><strong>Tester le workflow</strong><small>{logs.length ? 'Premier traitement enregistré' : 'Envoyez un email fictif'}</small></span><b>→</b></button>
          </div>
        </article>

        <article className="surface workflow-card">
          <div className="workflow-card-head"><span className="workflow-symbol">⌁</span><span className="status success">Publié</span></div>
          <p className="overline">n8n</p><h2>Classement d’un email fictif</h2><p>Webhook → moteur API → branche classée ou non classée → historique.</p>
          <div className="workflow-steps"><span>Webhook</span><i>→</i><span>API</span><i>→</i><span>Décision</span></div>
          <button className="button secondary" onClick={() => onNavigate('test')}>Ouvrir le banc de test</button>
        </article>
      </section>

      <HistoryTable logs={logs} onRefresh={onRefreshHistory} busy={busy} compact />
    </div>
  )
}
