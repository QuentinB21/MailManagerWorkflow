import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { api } from './api'
import { AppShell, type AppView } from './components/AppShell'
import { HistoryTable } from './components/HistoryTable'
import { LabelsView, type LabelFormState } from './components/LabelsView'
import { RulesView, type RuleFormState } from './components/RulesView'
import { WorkflowTestView, type EmailFormState } from './components/WorkflowTestView'
import { MailboxConnectionView } from './components/MailboxConnectionView'
import type { ClassificationResult, GmailOAuthConfiguration, GmailSyncResult, Label, Mailbox, ProcessingLog, Rule } from './types'

type ClassificationSection = 'rules' | 'destinations' | 'test'

const splitValues = (value: string) => value.split(',').map((item) => item.trim()).filter(Boolean)
const newExternalMessageId = () => `demo-${Date.now()}`
const emptyLabelForm = (): LabelFormState => ({ name: '', color: '#4f46e5', isActive: true })
const emptyRuleForm = (labelId = ''): RuleFormState => ({
  name: '',
  destinationLabelId: labelId,
  priority: 10,
  isActive: true,
  matchMode: 'Any',
  senderAddresses: '',
  senderDomains: '',
  subjectKeywords: '',
  bodyKeywords: '',
})
const defaultEmailForm = (): EmailFormState => ({
  externalMessageId: newExternalMessageId(),
  sender: 'contact@client.fr',
  subject: 'Point hebdomadaire Projet Alpha',
  body: 'Bonjour, voici les prochaines étapes.',
})

function App() {
  const [activeView, setActiveView] = useState<AppView>('classification')
  const [classificationSection, setClassificationSection] = useState<ClassificationSection>('rules')
  const [mailbox, setMailbox] = useState<Mailbox>()
  const [gmailConfiguration, setGmailConfiguration] = useState<GmailOAuthConfiguration>()
  const [labels, setLabels] = useState<Label[]>([])
  const [rules, setRules] = useState<Rule[]>([])
  const [logs, setLogs] = useState<ProcessingLog[]>([])
  const [result, setResult] = useState<ClassificationResult>()
  const [resultSource, setResultSource] = useState<'simulation' | 'workflow'>()
  const [gmailSyncResult, setGmailSyncResult] = useState<GmailSyncResult>()
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

  async function loadMailbox() {
    const items = await api.mailboxes()
    const activeMailbox = items.find((item) => item.isActive)
    if (!activeMailbox) throw new Error('Aucune boîte active configurée.')
    setMailbox(activeMailbox)
    return activeMailbox
  }

  async function loadGmailConfiguration() {
    const configuration = await api.gmailConfiguration()
    setGmailConfiguration(configuration)
    return configuration
  }

  useEffect(() => {
    const query = new URLSearchParams(window.location.search)
    const isGmailReturn = query.has('gmail') || query.has('gmailError')
    if (query.get('gmail') === 'connected') {
      setActiveView('settings')
      setNotice('Compte Gmail connecté avec succès.')
    } else if (query.has('gmailError')) {
      setActiveView('settings')
      setError('La connexion Gmail n’a pas abouti. Vérifiez la configuration Google puis réessayez.')
    }
    if (query.has('gmail') || query.has('gmailError')) {
      window.history.replaceState({}, '', window.location.pathname)
    }

    Promise.all([loadMailbox(), loadGmailConfiguration()])
      .then(([loadedMailbox]) => {
        if (!isGmailReturn && !loadedMailbox.isConnected) setActiveView('settings')
      })
      .catch((err: Error) => setError(err.message))
  }, [])

  useEffect(() => {
    if (!mailbox) return
    Promise.all([api.labels(mailbox.id), api.rules(mailbox.id), api.logs(mailbox.id)])
      .then(([labelItems, ruleItems, logItems]) => {
        setLabels(labelItems)
        setRules(ruleItems)
        setLogs(logItems)
        setRuleForm((current) => ({ ...current, destinationLabelId: current.destinationLabelId || labelItems[0]?.id || '' }))
      })
      .catch((err: Error) => setError(err.message))
  }, [mailbox])

  useEffect(() => {
    if (!notice) return
    const timeout = window.setTimeout(() => setNotice(''), 3500)
    return () => window.clearTimeout(timeout)
  }, [notice])

  const firstAvailableLabelId = useMemo(() => labels.find((label) => label.isActive)?.id ?? labels[0]?.id ?? '', [labels])

  async function refreshHistory() {
    if (!mailbox) return
    setBusy(true)
    try {
      setLogs(await api.logs(mailbox.id))
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  async function submitLabel(event: FormEvent) {
    event.preventDefault()
    if (!mailbox) return
    setBusy(true); setError('')
    try {
      const payload = { mailboxConnectionId: mailbox.id, name: labelForm.name, color: labelForm.color, isActive: labelForm.isActive }
      if (editingLabelId) {
        const existing = labels.find((label) => label.id === editingLabelId)
        await api.updateLabel(editingLabelId, { ...payload, externalLabelId: existing?.externalLabelId })
        setNotice('Label modifié avec succès.')
      } else {
        const created = await api.createLabel(payload)
        if (!ruleForm.destinationLabelId) setRuleForm((current) => ({ ...current, destinationLabelId: created.id }))
        setNotice('Label créé avec succès.')
      }
      setLabels(await api.labels(mailbox.id))
      cancelLabelEdit()
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  function editLabel(label: Label) {
    setEditingLabelId(label.id)
    setPendingDeleteLabelId(undefined)
    setLabelForm({ name: label.name, color: label.color || '#64748b', isActive: label.isActive })
    setLabelEditorOpen(true)
  }

  function createLabel() {
    setEditingLabelId(undefined)
    setPendingDeleteLabelId(undefined)
    setLabelForm(emptyLabelForm())
    setLabelEditorOpen(true)
  }

  function cancelLabelEdit() {
    setEditingLabelId(undefined)
    setLabelForm(emptyLabelForm())
    setLabelEditorOpen(false)
  }

  async function toggleLabel(label: Label) {
    setBusy(true); setError('')
    try {
      const { id, ...payload } = label
      await api.updateLabel(id, { ...payload, isActive: !label.isActive })
      setLabels(await api.labels(label.mailboxConnectionId))
      setNotice(label.isActive ? 'Label désactivé.' : 'Label activé.')
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  async function deleteLabel(label: Label) {
    setBusy(true); setError('')
    try {
      await api.deleteLabel(label.id)
      const updatedLabels = await api.labels(label.mailboxConnectionId)
      setLabels(updatedLabels)
      setPendingDeleteLabelId(undefined)
      if (editingLabelId === label.id) cancelLabelEdit()
      if (ruleForm.destinationLabelId === label.id) setRuleForm((current) => ({ ...current, destinationLabelId: updatedLabels[0]?.id || '' }))
      setNotice('Label supprimé.')
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  async function submitRule(event: FormEvent) {
    event.preventDefault()
    if (!mailbox || !ruleForm.destinationLabelId) return
    setBusy(true); setError('')
    try {
      const payload = {
        mailboxConnectionId: mailbox.id,
        destinationLabelId: ruleForm.destinationLabelId,
        name: ruleForm.name,
        priority: Number(ruleForm.priority),
        isActive: ruleForm.isActive,
        matchMode: ruleForm.matchMode,
        senderAddresses: splitValues(ruleForm.senderAddresses),
        senderDomains: splitValues(ruleForm.senderDomains),
        subjectKeywords: splitValues(ruleForm.subjectKeywords),
        bodyKeywords: splitValues(ruleForm.bodyKeywords),
      }
      if (editingRuleId) {
        await api.updateRule(editingRuleId, payload)
        setNotice('Règle modifiée avec succès.')
      } else {
        await api.createRule(payload)
        setNotice('Règle créée avec succès.')
      }
      setRules(await api.rules(mailbox.id))
      cancelRuleEdit()
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  function editRule(rule: Rule) {
    setEditingRuleId(rule.id)
    setPendingDeleteRuleId(undefined)
    setRuleForm({
      name: rule.name,
      destinationLabelId: rule.destinationLabelId,
      priority: rule.priority,
      isActive: rule.isActive,
      matchMode: rule.matchMode,
      senderAddresses: rule.senderAddresses.join(', '),
      senderDomains: rule.senderDomains.join(', '),
      subjectKeywords: rule.subjectKeywords.join(', '),
      bodyKeywords: rule.bodyKeywords.join(', '),
    })
    setRuleEditorOpen(true)
  }

  function createRule() {
    setEditingRuleId(undefined)
    setPendingDeleteRuleId(undefined)
    setRuleForm(emptyRuleForm(firstAvailableLabelId))
    setRuleEditorOpen(true)
  }

  function cancelRuleEdit() {
    setEditingRuleId(undefined)
    setRuleForm(emptyRuleForm(firstAvailableLabelId))
    setRuleEditorOpen(false)
  }

  async function toggleRule(rule: Rule) {
    setBusy(true); setError('')
    try {
      const { id, destinationLabelName: _, ...payload } = rule
      await api.updateRule(id, { ...payload, isActive: !rule.isActive })
      if (mailbox) setRules(await api.rules(mailbox.id))
      setNotice(rule.isActive ? 'Règle désactivée.' : 'Règle activée.')
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  async function deleteRule(rule: Rule) {
    setBusy(true); setError('')
    try {
      await api.deleteRule(rule.id)
      if (mailbox) setRules(await api.rules(mailbox.id))
      setPendingDeleteRuleId(undefined)
      if (editingRuleId === rule.id) cancelRuleEdit()
      setNotice('Règle supprimée.')
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  async function simulate(event: FormEvent) {
    event.preventDefault()
    if (!mailbox) return
    setBusy(true); setError('')
    try {
      setResult(await api.simulate({ mailboxConnectionId: mailbox.id, ...emailForm }))
      setResultSource('simulation')
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  async function runWorkflow() {
    if (!mailbox) return
    setBusy(true); setError('')
    try {
      setResult(await api.runWorkflow({ mailboxConnectionId: mailbox.id, ...emailForm }))
      setResultSource('workflow')
      setLogs(await api.logs(mailbox.id))
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  function connectGmail() {
    if (!mailbox) return
    window.location.assign(api.gmailAuthorizationUrl(mailbox.id))
  }

  async function testGmailConnection() {
    if (!mailbox) return
    setBusy(true); setError('')
    try {
      const status = await api.testGmailConnection(mailbox.id)
      setNotice(`Connexion Gmail valide pour ${status.emailAddress}.`)
      await loadMailbox()
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  async function syncGmail(maxResults: number) {
    if (!mailbox) return
    setBusy(true); setError('')
    try {
      const syncResult = await api.syncGmail(mailbox.id, maxResults)
      setGmailSyncResult(syncResult)
      setLogs(await api.logs(mailbox.id))
      await loadMailbox()
      setNotice(`${syncResult.processedCount} email(s) traité(s), ${syncResult.labelAppliedCount} label(s) appliqué(s).`)
    } catch (err) {
      setError((err as Error).message)
      await loadMailbox().catch(() => undefined)
    } finally {
      setBusy(false)
    }
  }

  async function disconnectGmail() {
    if (!mailbox) return
    setBusy(true); setError('')
    try {
      await api.disconnectGmail(mailbox.id)
      setGmailSyncResult(undefined)
      await loadMailbox()
      setNotice('Le compte Gmail a été déconnecté et son accès révoqué.')
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <AppShell activeView={activeView} mailbox={mailbox} onNavigate={setActiveView}>
      {error && <div className="global-message error" role="alert"><span>!</span><p>{error}</p><button onClick={() => setError('')} aria-label="Fermer">×</button></div>}
      {notice && <div className="global-message notice" role="status"><span>✓</span><p>{notice}</p><button onClick={() => setNotice('')} aria-label="Fermer">×</button></div>}

      {activeView === 'classification' && (
        <div className="page">
          <div className="page-header"><div><p className="overline">Classement automatique</p><h1>Classement</h1><p>Définissez où vont vos emails et les conditions qui déclenchent chaque destination.</p></div></div>
          <div className="segmented-nav" role="tablist" aria-label="Gestion du classement">
            <button role="tab" aria-selected={classificationSection === 'rules'} className={classificationSection === 'rules' ? 'active' : ''} onClick={() => setClassificationSection('rules')}>Règles <span>{rules.length}</span></button>
            <button role="tab" aria-selected={classificationSection === 'destinations'} className={classificationSection === 'destinations' ? 'active' : ''} onClick={() => setClassificationSection('destinations')}>Destinations <span>{labels.length}</span></button>
            <button role="tab" aria-selected={classificationSection === 'test'} className={classificationSection === 'test' ? 'active' : ''} onClick={() => setClassificationSection('test')}>Tester les règles</button>
          </div>
          {classificationSection === 'destinations' ? (
            <LabelsView labels={labels} rules={rules} form={labelForm} editorOpen={labelEditorOpen} editingId={editingLabelId} pendingDeleteId={pendingDeleteLabelId} busy={busy} onFormChange={setLabelForm} onCreate={createLabel} onSubmit={submitLabel} onEdit={editLabel} onCancelEdit={cancelLabelEdit} onToggle={toggleLabel} onRequestDelete={setPendingDeleteLabelId} onDelete={deleteLabel} />
          ) : classificationSection === 'rules' ? (
            <RulesView rules={rules} labels={labels} form={ruleForm} editorOpen={ruleEditorOpen} editingId={editingRuleId} pendingDeleteId={pendingDeleteRuleId} busy={busy} onFormChange={setRuleForm} onCreate={createRule} onSubmit={submitRule} onEdit={editRule} onCancelEdit={cancelRuleEdit} onToggle={toggleRule} onRequestDelete={setPendingDeleteRuleId} onDelete={deleteRule} />
          ) : (
            <WorkflowTestView embedded form={emailForm} result={result} resultSource={resultSource} busy={busy} onFormChange={setEmailForm} onGenerateId={() => setEmailForm((current) => ({ ...current, externalMessageId: newExternalMessageId() }))} onSimulate={simulate} onRunWorkflow={runWorkflow} />
          )}
        </div>
      )}

      {activeView === 'activity' && (
        <div className="page">
          <div className="page-header"><div><p className="overline">Suivi des traitements</p><h1>Activité</h1><p>Contrôlez les décisions récentes et repérez immédiatement les erreurs Gmail.</p></div></div>
          <HistoryTable logs={logs} onRefresh={refreshHistory} busy={busy} />
        </div>
      )}

      {activeView === 'settings' && mailbox && gmailConfiguration && <MailboxConnectionView mailbox={mailbox} configuration={gmailConfiguration} busy={busy} syncResult={gmailSyncResult} onConnect={connectGmail} onTestConnection={testGmailConnection} onSync={syncGmail} onDisconnect={disconnectGmail} />}
    </AppShell>
  )
}

export default App
