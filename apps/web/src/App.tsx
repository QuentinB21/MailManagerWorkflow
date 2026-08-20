import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { api } from './api'
import { AppShell, type AppView } from './components/AppShell'
import { HistoryTable } from './components/HistoryTable'
import { LabelsView, type LabelFormState } from './components/LabelsView'
import { createRuleCondition, RulesView, type RuleCondition, type RuleFormState } from './components/RulesView'
import { WorkflowTestView, type EmailFormState } from './components/WorkflowTestView'
import { MailboxConnectionView } from './components/MailboxConnectionView'
import type { ClassificationResult, GmailOAuthConfiguration, Label, Mailbox, MailboxSyncResult, MailProvider, ProcessingLog, ProviderConfiguration, Rule } from './types'

type ClassificationSection = 'rules' | 'destinations' | 'test'
const splitValues = (value: string) => value.split(',').map((item) => item.trim()).filter(Boolean)
const newExternalMessageId = () => `demo-${Date.now()}`
const emptyLabelForm = (): LabelFormState => ({ name: '', color: '#4f46e5', isActive: true })
const emptyRuleForm = (labelId = ''): RuleFormState => ({ name: '', destinationLabelId: labelId, priority: 10, isActive: true, matchMode: 'Any', conditions: [] })
const ruleConditions = (rule: Rule): RuleCondition[] => [
  ...rule.senderAddresses.map((value) => createRuleCondition('senderAddress', value)),
  ...rule.senderDomains.map((value) => createRuleCondition('senderDomain', value)),
  ...rule.subjectKeywords.map((value) => createRuleCondition('subjectKeyword', value)),
  ...rule.bodyKeywords.map((value) => createRuleCondition('bodyKeyword', value)),
]
const defaultEmailForm = (): EmailFormState => ({ externalMessageId: newExternalMessageId(), sender: 'contact@client.fr', subject: 'Point hebdomadaire Projet Alpha', body: 'Bonjour, voici les prochaines étapes.' })

function App() {
  const [activeView, setActiveView] = useState<AppView>('classification')
  const [classificationSection, setClassificationSection] = useState<ClassificationSection>('destinations')
  const [mailboxes, setMailboxes] = useState<Mailbox[]>([])
  const [mailbox, setMailbox] = useState<Mailbox>()
  const [gmailConfiguration, setGmailConfiguration] = useState<GmailOAuthConfiguration>()
  const [outlookConfiguration, setOutlookConfiguration] = useState<ProviderConfiguration>()
  const [labels, setLabels] = useState<Label[]>([])
  const [rules, setRules] = useState<Rule[]>([])
  const [logs, setLogs] = useState<ProcessingLog[]>([])
  const [result, setResult] = useState<ClassificationResult>()
  const [resultSource, setResultSource] = useState<'simulation' | 'workflow'>()
  const [mailboxSyncResult, setMailboxSyncResult] = useState<MailboxSyncResult>()
  const [labelForm, setLabelForm] = useState<LabelFormState>(emptyLabelForm)
  const [ruleForm, setRuleForm] = useState<RuleFormState>(() => emptyRuleForm())
  const [emailForm, setEmailForm] = useState<EmailFormState>(defaultEmailForm)
  const [editingLabelId, setEditingLabelId] = useState<string>()
  const [editingRuleId, setEditingRuleId] = useState<string>()
  const [labelEditorOpen, setLabelEditorOpen] = useState(false)
  const [ruleEditorOpen, setRuleEditorOpen] = useState(false)
  const [pendingDeleteLabelId, setPendingDeleteLabelId] = useState<string>()
  const [pendingDeleteRuleId, setPendingDeleteRuleId] = useState<string>()
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  async function loadMailboxes(preferredId?: string) {
    const items = await api.mailboxes()
    setMailboxes(items)
    const selected = items.find((item) => item.id === (preferredId ?? mailbox?.id)) ?? items.find((item) => item.isActive)
    if (!selected) throw new Error('Aucune boîte active configurée.')
    setMailbox(selected)
    return selected
  }

  async function loadConfigurations() {
    const [gmail, outlook] = await Promise.all([api.gmailConfiguration(), api.outlookConfiguration()])
    setGmailConfiguration(gmail); setOutlookConfiguration(outlook)
  }

  useEffect(() => {
    const query = new URLSearchParams(window.location.search)
    const oauthReturn = query.has('gmail') || query.has('gmailError') || query.has('outlook') || query.has('outlookError')
    const returnedMailboxId = query.get('mailboxId') ?? undefined
    if (query.get('gmail') === 'connected') { setActiveView('settings'); setNotice('Compte Gmail connecté avec succès.') }
    else if (query.has('gmailError')) { setActiveView('settings'); setError('La connexion Gmail n’a pas abouti. Vérifiez la configuration Google puis réessayez.') }
    else if (query.get('outlook') === 'connected') { setActiveView('settings'); setNotice('Compte Outlook connecté avec succès.') }
    else if (query.has('outlookError')) { setActiveView('settings'); setError('La connexion Outlook n’a pas abouti. Vérifiez la configuration Microsoft puis réessayez.') }
    if (oauthReturn) window.history.replaceState({}, '', window.location.pathname)
    Promise.all([loadMailboxes(returnedMailboxId), loadConfigurations()])
      .then(([selected]) => { if (!oauthReturn && !selected.isConnected) setActiveView('settings') })
      .catch((err: Error) => setError(err.message))
  }, [])

  useEffect(() => {
    if (!mailbox) return
    setMailboxSyncResult(undefined); setResult(undefined); setEditingLabelId(undefined); setEditingRuleId(undefined)
    setLabelEditorOpen(false); setRuleEditorOpen(false); setRuleForm(emptyRuleForm())
    Promise.all([api.labels(mailbox.id), api.rules(mailbox.id), api.logs(mailbox.id)])
      .then(([labelItems, ruleItems, logItems]) => {
        setLabels(labelItems); setRules(ruleItems); setLogs(logItems)
        setRuleForm(emptyRuleForm(labelItems.find((item) => item.isActive)?.id ?? labelItems[0]?.id ?? ''))
      })
      .catch((err: Error) => setError(err.message))
  }, [mailbox?.id])

  useEffect(() => {
    if (!notice) return
    const timeout = window.setTimeout(() => setNotice(''), 3500)
    return () => window.clearTimeout(timeout)
  }, [notice])

  const firstAvailableLabelId = useMemo(() => labels.find((item) => item.isActive)?.id ?? labels[0]?.id ?? '', [labels])
  const configurations: Record<MailProvider, ProviderConfiguration> = {
    Gmail: gmailConfiguration ?? { isConfigured: false, source: 'None' },
    Outlook: outlookConfiguration ?? { isConfigured: false, source: 'None' },
  }

  function selectMailbox(id: string) {
    const selected = mailboxes.find((item) => item.id === id)
    if (selected) setMailbox(selected)
  }

  async function refreshHistory() {
    if (!mailbox) return
    setBusy(true); try { setLogs(await api.logs(mailbox.id)) } catch (err) { setError((err as Error).message) } finally { setBusy(false) }
  }

  async function submitLabel(event: FormEvent) {
    event.preventDefault(); if (!mailbox) return
    setBusy(true); setError('')
    try {
      const payload = { mailboxConnectionId: mailbox.id, name: labelForm.name, color: labelForm.color, isActive: labelForm.isActive }
      if (editingLabelId) {
        const existing = labels.find((item) => item.id === editingLabelId)
        await api.updateLabel(editingLabelId, { ...payload, externalLabelId: existing?.externalLabelId }); setNotice('Destination modifiée avec succès.')
      } else {
        const created = await api.createLabel(payload); if (!ruleForm.destinationLabelId) setRuleForm((current) => ({ ...current, destinationLabelId: created.id })); setNotice('Destination créée avec succès.')
      }
      setLabels(await api.labels(mailbox.id)); cancelLabelEdit()
    } catch (err) { setError((err as Error).message) } finally { setBusy(false) }
  }

  function editLabel(label: Label) { setEditingLabelId(label.id); setPendingDeleteLabelId(undefined); setLabelForm({ name: label.name, color: label.color || '#64748b', isActive: label.isActive }); setLabelEditorOpen(true) }
  function createLabel() { setEditingLabelId(undefined); setPendingDeleteLabelId(undefined); setLabelForm(emptyLabelForm()); setLabelEditorOpen(true) }
  function cancelLabelEdit() { setEditingLabelId(undefined); setLabelForm(emptyLabelForm()); setLabelEditorOpen(false) }
  async function toggleLabel(label: Label) {
    setBusy(true); setError(''); try { const { id, ...payload } = label; await api.updateLabel(id, { ...payload, isActive: !label.isActive }); setLabels(await api.labels(label.mailboxConnectionId)); setNotice(label.isActive ? 'Destination désactivée.' : 'Destination activée.') } catch (err) { setError((err as Error).message) } finally { setBusy(false) }
  }
  async function deleteLabel(label: Label) {
    setBusy(true); setError(''); try { await api.deleteLabel(label.id); const updated = await api.labels(label.mailboxConnectionId); setLabels(updated); setPendingDeleteLabelId(undefined); if (editingLabelId === label.id) cancelLabelEdit(); if (ruleForm.destinationLabelId === label.id) setRuleForm((current) => ({ ...current, destinationLabelId: updated[0]?.id || '' })); setNotice('Destination supprimée.') } catch (err) { setError((err as Error).message) } finally { setBusy(false) }
  }

  async function submitRule(event: FormEvent) {
    event.preventDefault(); if (!mailbox || !ruleForm.destinationLabelId) return
    setBusy(true); setError('')
    try {
      const valuesFor = (type: RuleCondition['type']) => ruleForm.conditions.filter((condition) => condition.type === type).flatMap((condition) => splitValues(condition.value))
      const payload = { mailboxConnectionId: mailbox.id, destinationLabelId: ruleForm.destinationLabelId, name: ruleForm.name, priority: Number(ruleForm.priority), isActive: ruleForm.isActive, matchMode: ruleForm.matchMode, senderAddresses: valuesFor('senderAddress'), senderDomains: valuesFor('senderDomain'), subjectKeywords: valuesFor('subjectKeyword'), bodyKeywords: valuesFor('bodyKeyword') }
      if (editingRuleId) { await api.updateRule(editingRuleId, payload); setNotice('Règle modifiée avec succès.') } else { await api.createRule(payload); setNotice('Règle créée avec succès.') }
      setRules(await api.rules(mailbox.id)); cancelRuleEdit()
    } catch (err) { setError((err as Error).message) } finally { setBusy(false) }
  }
  function editRule(rule: Rule) { setEditingRuleId(rule.id); setPendingDeleteRuleId(undefined); setRuleForm({ name: rule.name, destinationLabelId: rule.destinationLabelId, priority: rule.priority, isActive: rule.isActive, matchMode: rule.matchMode, conditions: ruleConditions(rule) }); setRuleEditorOpen(true) }
  function createRule() { setEditingRuleId(undefined); setPendingDeleteRuleId(undefined); setRuleForm(emptyRuleForm(firstAvailableLabelId)); setRuleEditorOpen(true) }
  function cancelRuleEdit() { setEditingRuleId(undefined); setRuleForm(emptyRuleForm(firstAvailableLabelId)); setRuleEditorOpen(false) }
  async function toggleRule(rule: Rule) { setBusy(true); setError(''); try { const { id, destinationLabelName: _, ...payload } = rule; await api.updateRule(id, { ...payload, isActive: !rule.isActive }); if (mailbox) setRules(await api.rules(mailbox.id)); setNotice(rule.isActive ? 'Règle désactivée.' : 'Règle activée.') } catch (err) { setError((err as Error).message) } finally { setBusy(false) } }
  async function deleteRule(rule: Rule) { setBusy(true); setError(''); try { await api.deleteRule(rule.id); if (mailbox) setRules(await api.rules(mailbox.id)); setPendingDeleteRuleId(undefined); if (editingRuleId === rule.id) cancelRuleEdit(); setNotice('Règle supprimée.') } catch (err) { setError((err as Error).message) } finally { setBusy(false) } }

  async function simulate(event: FormEvent) { event.preventDefault(); if (!mailbox) return; setBusy(true); setError(''); try { setResult(await api.simulate({ mailboxConnectionId: mailbox.id, ...emailForm })); setResultSource('simulation') } catch (err) { setError((err as Error).message) } finally { setBusy(false) } }
  async function runWorkflow() { if (!mailbox) return; setBusy(true); setError(''); try { setResult(await api.runWorkflow({ mailboxConnectionId: mailbox.id, ...emailForm })); setResultSource('workflow'); setLogs(await api.logs(mailbox.id)) } catch (err) { setError((err as Error).message) } finally { setBusy(false) } }

  async function addMailbox(provider: MailProvider) {
    setBusy(true); setError(''); try { const created = await api.createMailbox(provider); await loadMailboxes(created.id); setMailboxSyncResult(undefined); setNotice(`Connexion ${provider} ajoutée. Vous pouvez maintenant l’autoriser.`) } catch (err) { setError((err as Error).message) } finally { setBusy(false) }
  }
  function connectMailbox() { if (!mailbox) return; window.location.assign(mailbox.provider === 'Gmail' ? api.gmailAuthorizationUrl(mailbox.id) : api.outlookAuthorizationUrl(mailbox.id)) }
  async function testMailboxConnection() { if (!mailbox) return; setBusy(true); setError(''); try { const status = mailbox.provider === 'Gmail' ? await api.testGmailConnection(mailbox.id) : await api.testOutlookConnection(mailbox.id); setNotice(`Connexion ${mailbox.provider} valide pour ${status.emailAddress}.`); await loadMailboxes(mailbox.id) } catch (err) { setError((err as Error).message) } finally { setBusy(false) } }
  async function syncMailbox(maxResults: number) { if (!mailbox) return; setBusy(true); setError(''); try { const sync = await api.syncMailbox(mailbox.id, maxResults); setMailboxSyncResult(sync); setLogs(await api.logs(mailbox.id)); await loadMailboxes(mailbox.id); setNotice(`${sync.processedCount} email(s) traité(s), ${sync.destinationAppliedCount} destination(s) appliquée(s).`) } catch (err) { setError((err as Error).message); await loadMailboxes(mailbox.id).catch(() => undefined) } finally { setBusy(false) } }
  async function disconnectMailbox() { if (!mailbox) return; setBusy(true); setError(''); try { if (mailbox.provider === 'Gmail') await api.disconnectGmail(mailbox.id); else await api.disconnectOutlook(mailbox.id); setMailboxSyncResult(undefined); await loadMailboxes(mailbox.id); setNotice(`Le compte ${mailbox.provider} a été déconnecté.`) } catch (err) { setError((err as Error).message) } finally { setBusy(false) } }
  async function deleteMailbox() { if (!mailbox) return; setBusy(true); setError(''); try { await api.deleteMailbox(mailbox.id); const items = await api.mailboxes(); setMailboxes(items); setMailbox(items[0]); setNotice('Entrée de boîte supprimée.') } catch (err) { setError((err as Error).message) } finally { setBusy(false) } }

  return (
    <AppShell activeView={activeView} mailbox={mailbox} mailboxes={mailboxes} onSelectMailbox={selectMailbox} onNavigate={setActiveView}>
      {error && <div className="global-message error" role="alert"><span>!</span><p>{error}</p><button onClick={() => setError('')} aria-label="Fermer">×</button></div>}
      {notice && <div className="global-message notice" role="status"><span>✓</span><p>{notice}</p><button onClick={() => setNotice('')} aria-label="Fermer">×</button></div>}
      {activeView === 'classification' && <div className="page">
        <div className="page-header"><div><p className="overline">{mailbox?.emailAddress ?? mailbox?.displayName}</p><h1>Classement</h1><p>Règles et destinations exclusivement appliquées à cette boîte {mailbox?.provider}.</p></div></div>
        <div className="segmented-nav" role="tablist" aria-label="Gestion du classement"><button role="tab" aria-selected={classificationSection === 'destinations'} className={classificationSection === 'destinations' ? 'active' : ''} onClick={() => setClassificationSection('destinations')}>Destinations <span>{labels.length}</span></button><button role="tab" aria-selected={classificationSection === 'rules'} className={classificationSection === 'rules' ? 'active' : ''} onClick={() => setClassificationSection('rules')}>Règles <span>{rules.length}</span></button><button role="tab" aria-selected={classificationSection === 'test'} className={classificationSection === 'test' ? 'active' : ''} onClick={() => setClassificationSection('test')}>Tester les règles</button></div>
        {classificationSection === 'destinations' ? <LabelsView labels={labels} rules={rules} form={labelForm} editorOpen={labelEditorOpen} editingId={editingLabelId} pendingDeleteId={pendingDeleteLabelId} busy={busy} onFormChange={setLabelForm} onCreate={createLabel} onSubmit={submitLabel} onEdit={editLabel} onCancelEdit={cancelLabelEdit} onToggle={toggleLabel} onRequestDelete={setPendingDeleteLabelId} onDelete={deleteLabel} /> : classificationSection === 'rules' ? <RulesView rules={rules} labels={labels} form={ruleForm} editorOpen={ruleEditorOpen} editingId={editingRuleId} pendingDeleteId={pendingDeleteRuleId} busy={busy} onFormChange={setRuleForm} onCreate={createRule} onSubmit={submitRule} onEdit={editRule} onCancelEdit={cancelRuleEdit} onToggle={toggleRule} onRequestDelete={setPendingDeleteRuleId} onDelete={deleteRule} /> : <WorkflowTestView embedded form={emailForm} result={result} resultSource={resultSource} busy={busy} onFormChange={setEmailForm} onGenerateId={() => setEmailForm((current) => ({ ...current, externalMessageId: newExternalMessageId() }))} onSimulate={simulate} onRunWorkflow={runWorkflow} />}
      </div>}
      {activeView === 'activity' && <div className="page"><div className="page-header"><div><p className="overline">{mailbox?.emailAddress ?? mailbox?.displayName}</p><h1>Activité</h1><p>Décisions et actions fournisseur pour la boîte {mailbox?.provider} sélectionnée.</p></div></div><HistoryTable logs={logs} onRefresh={refreshHistory} busy={busy} /></div>}
      {activeView === 'settings' && mailbox && <MailboxConnectionView mailboxes={mailboxes} selectedMailbox={mailbox} configurations={configurations} busy={busy} syncResult={mailboxSyncResult} onSelect={selectMailbox} onAdd={addMailbox} onConnect={connectMailbox} onTestConnection={testMailboxConnection} onSync={syncMailbox} onDisconnect={disconnectMailbox} onDelete={deleteMailbox} />}
    </AppShell>
  )
}

export default App
