import type { ProcessingLog } from '../types'

type Props = {
  logs: ProcessingLog[]
  onRefresh: () => void
  busy?: boolean
  compact?: boolean
}

export function HistoryTable({ logs, onRefresh, busy, compact }: Props) {
  const visibleLogs = compact ? logs.slice(0, 6) : logs

  return (
    <section className="surface history-surface">
      <div className="section-header">
        <div><p className="overline">Activité</p><h2>Traitements récents</h2><p>Décisions enregistrées par le workflow n8n.</p></div>
        <button type="button" className="button ghost" disabled={busy} onClick={onRefresh}>Actualiser</button>
      </div>

      {visibleLogs.length === 0 ? (
        <div className="empty-state compact"><span className="empty-icon">↗</span><h3>Aucun traitement</h3><p>Testez un email via n8n pour alimenter l’historique.</p></div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead><tr><th>Date</th><th>Message</th><th>Décision</th><th>Action Gmail</th><th>Explication</th></tr></thead>
            <tbody>{visibleLogs.map((log) => (
              <tr key={log.id}>
                <td><span className="cell-primary">{new Date(log.processedAt).toLocaleDateString('fr-FR')}</span><small>{new Date(log.processedAt).toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit' })}</small></td>
                <td><code>{log.externalMessageId}</code></td>
                <td><span className={log.isClassified ? 'status success' : 'status neutral'}>{log.isClassified ? log.destinationLabelName : 'Non classé'}</span></td>
                <td>
                  {log.providerLabelAppliedAt ? (
                    <span className="status success">Label appliqué</span>
                  ) : log.providerActionError ? (
                    <span className="status error" title={log.providerActionError}>Échec Gmail</span>
                  ) : (
                    <span className="status neutral">Non demandé</span>
                  )}
                </td>
                <td className="explanation-cell">{log.matchedCriteria.join(', ') || log.noMatchReason}</td>
              </tr>
            ))}</tbody>
          </table>
        </div>
      )}
    </section>
  )
}
