import type { ClassificationResult, GmailOAuthConfiguration, Label, Mailbox, MailboxSyncResult, MailProvider, ProcessingLog, ProviderConfiguration, Rule, WorkflowResult } from './types'
import { getAccessToken } from './auth'

const baseUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:8080'
const workflowUrl = import.meta.env.VITE_N8N_WEBHOOK_URL ?? 'http://localhost:5678/webhook/mail-manager/email'
type RulePayload = Omit<Rule, 'id' | 'destinationLabelName'>
type LabelPayload = Omit<Label, 'id'>

async function requestUrl<T>(url: string, options?: RequestInit): Promise<T> {
  const token = await getAccessToken()
  const response = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options?.headers,
    },
  })
  if (!response.ok) {
    const payload = await response.json().catch(() => null)
    throw new Error(payload?.error ?? payload?.detail ?? `Erreur API (${response.status})`)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

const request = <T>(path: string, options?: RequestInit) => requestUrl<T>(`${baseUrl}${path}`, options)

export const api = {
  mailboxes: () => request<Mailbox[]>('/api/mailboxes'),
  createMailbox: (provider: MailProvider) =>
    request<Mailbox>('/api/mailboxes', { method: 'POST', body: JSON.stringify({ provider }) }),
  deleteMailbox: (mailboxId: string) => request<void>(`/api/mailboxes/${mailboxId}`, { method: 'DELETE' }),
  labels: (mailboxId: string) => request<Label[]>(`/api/labels?mailboxConnectionId=${mailboxId}`),
  rules: (mailboxId: string) => request<Rule[]>(`/api/rules?mailboxConnectionId=${mailboxId}`),
  logs: (mailboxId: string) => request<ProcessingLog[]>(`/api/processing-logs?mailboxConnectionId=${mailboxId}&limit=20`),
  createRule: (rule: RulePayload) => request<Rule>('/api/rules', { method: 'POST', body: JSON.stringify(rule) }),
  updateRule: (id: string, rule: RulePayload) => request<Rule>(`/api/rules/${id}`, { method: 'PUT', body: JSON.stringify(rule) }),
  deleteRule: (id: string) => request<void>(`/api/rules/${id}`, { method: 'DELETE' }),
  createLabel: (label: LabelPayload) => request<Label>('/api/labels', { method: 'POST', body: JSON.stringify(label) }),
  updateLabel: (id: string, label: LabelPayload) => request<Label>(`/api/labels/${id}`, { method: 'PUT', body: JSON.stringify(label) }),
  deleteLabel: (id: string) => request<void>(`/api/labels/${id}`, { method: 'DELETE' }),
  gmailConfiguration: () => request<GmailOAuthConfiguration>('/api/gmail/configuration'),
  gmailAuthorizationUrl: (mailboxId: string) => request<{ url: string }>(`/api/gmail/oauth/authorization-url?mailboxConnectionId=${encodeURIComponent(mailboxId)}`),
  testGmailConnection: (mailboxId: string) => request<{ isConnected: boolean; emailAddress: string }>(`/api/gmail/mailboxes/${mailboxId}/test`),
  disconnectGmail: (mailboxId: string) => request<void>(`/api/gmail/mailboxes/${mailboxId}/disconnect`, { method: 'POST' }),
  outlookConfiguration: () => request<ProviderConfiguration>('/api/outlook/configuration'),
  outlookAuthorizationUrl: (mailboxId: string) => request<{ url: string }>(`/api/outlook/oauth/authorization-url?mailboxConnectionId=${encodeURIComponent(mailboxId)}`),
  testOutlookConnection: (mailboxId: string) => request<{ isConnected: boolean; emailAddress: string }>(`/api/outlook/mailboxes/${mailboxId}/test`),
  disconnectOutlook: (mailboxId: string) => request<void>(`/api/outlook/mailboxes/${mailboxId}/disconnect`, { method: 'POST' }),
  syncMailbox: (mailboxId: string, maxResults: number) => request<MailboxSyncResult>(`/api/mailboxes/${mailboxId}/sync`, {
    method: 'POST',
    body: JSON.stringify({ maxResults }),
  }),
  simulate: (email: { mailboxConnectionId: string; externalMessageId: string; sender: string; subject: string; body: string }) =>
    request<ClassificationResult>('/api/classification/simulate', { method: 'POST', body: JSON.stringify(email) }),
  runWorkflow: (email: { mailboxConnectionId: string; externalMessageId: string; sender: string; subject: string; body: string }) =>
    requestUrl<WorkflowResult>(workflowUrl, { method: 'POST', body: JSON.stringify(email) }),
}
