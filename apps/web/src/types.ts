export type MailProvider = 'Gmail' | 'Outlook'

export type Mailbox = {
  id: string
  displayName: string
  provider: MailProvider
  isActive: boolean
  emailAddress?: string
  isConnected: boolean
  oAuthConfigured: boolean
  connectedAt?: string
  lastSyncAt?: string
  lastSyncError?: string
}

export type Label = {
  id: string
  mailboxConnectionId: string
  name: string
  externalLabelId?: string
  color?: string
  isActive: boolean
}

export type MatchMode = 'Any' | 'All'

export type Rule = {
  id: string
  mailboxConnectionId: string
  destinationLabelId: string
  destinationLabelName: string
  name: string
  priority: number
  isActive: boolean
  matchMode: MatchMode
  senderAddresses: string[]
  senderDomains: string[]
  subjectKeywords: string[]
  bodyKeywords: string[]
}

export type ClassificationResult = {
  isClassified: boolean
  label?: { id: string; name: string }
  matchedRule?: { id: string; name: string; priority: number }
  matchedCriteria: string[]
  noMatchReason?: string
  wasAlreadyProcessed: boolean
  processingLogId?: string
}

export type WorkflowResult = ClassificationResult & {
  workflowOutcome: 'classified' | 'unclassified'
}

export type ProcessingLog = {
  id: string
  externalMessageId: string
  subjectPreview?: string
  isClassified: boolean
  destinationLabelName?: string
  matchedRuleName?: string
  matchedCriteria: string[]
  noMatchReason?: string
  providerLabelAppliedAt?: string
  providerActionError?: string
  processedAt: string
}

export type MailboxSyncResult = {
  requestedCount: number
  discoveredCount: number
  processedCount: number
  classifiedCount: number
  destinationAppliedCount: number
  unclassifiedCount: number
  failureCount: number
  results: Array<{
    externalMessageId: string
    subject?: string
    isClassified: boolean
    label?: { id: string; name: string }
    matchedRule?: { id: string; name: string; priority: number }
    matchedCriteria: string[]
    noMatchReason?: string
    wasAlreadyProcessed: boolean
    destinationApplied: boolean
    error?: string
  }>
}

export type GmailOAuthConfiguration = {
  isConfigured: boolean
  source: 'Environment' | 'LegacyDatabase' | 'None'
}

export type ProviderConfiguration = {
  isConfigured: boolean
  source: string
}
