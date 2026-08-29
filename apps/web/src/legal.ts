export const legalPaths = {
  legalNotice: '/mentions-legales',
  privacy: '/politique-confidentialite',
  terms: '/conditions-utilisation',
  cookies: '/cookies',
} as const

export type LegalDocumentKind = keyof typeof legalPaths

const configured = (value: string | undefined, placeholder: string) => value?.trim() || `[À compléter : ${placeholder}]`

const publisherDisclosureMode = import.meta.env.VITE_LEGAL_PUBLISHER_DISCLOSURE_MODE === 'protected-non-professional'
  ? 'protected-non-professional'
  : 'identified'
const isProtectedNonProfessional = publisherDisclosureMode === 'protected-non-professional'

export const legalConfiguration = {
  publisherDisclosureMode,
  isProtectedNonProfessional,
  publisherName: configured(import.meta.env.VITE_LEGAL_PUBLISHER_NAME, "nom de l’éditeur"),
  publisherForm: configured(import.meta.env.VITE_LEGAL_PUBLISHER_FORM, 'forme juridique ou statut'),
  publisherAddress: configured(import.meta.env.VITE_LEGAL_PUBLISHER_ADDRESS, 'adresse postale'),
  publisherEmail: configured(import.meta.env.VITE_LEGAL_PUBLISHER_EMAIL, 'adresse de contact'),
  publisherPhone: configured(import.meta.env.VITE_LEGAL_PUBLISHER_PHONE, 'numéro de téléphone'),
  registrationNumber: configured(import.meta.env.VITE_LEGAL_REGISTRATION_NUMBER, 'SIREN/SIRET ou numéro d’immatriculation'),
  publicationDirector: configured(import.meta.env.VITE_LEGAL_PUBLICATION_DIRECTOR, 'directeur de la publication'),
  hostName: configured(import.meta.env.VITE_LEGAL_HOST_NAME, "nom de l’hébergeur"),
  hostAddress: configured(import.meta.env.VITE_LEGAL_HOST_ADDRESS, "adresse de l’hébergeur"),
  hostPhone: configured(import.meta.env.VITE_LEGAL_HOST_PHONE, "téléphone de l’hébergeur"),
  hostingLocation: configured(import.meta.env.VITE_LEGAL_HOSTING_LOCATION, "lieu d’hébergement des données"),
  privacyEmail: configured(import.meta.env.VITE_LEGAL_PRIVACY_EMAIL ?? import.meta.env.VITE_LEGAL_PUBLISHER_EMAIL, 'contact RGPD'),
  isComplete: Boolean(
    import.meta.env.VITE_LEGAL_HOST_NAME?.trim()
    && import.meta.env.VITE_LEGAL_HOST_ADDRESS?.trim()
    && import.meta.env.VITE_LEGAL_HOST_PHONE?.trim()
    && import.meta.env.VITE_LEGAL_PUBLISHER_NAME?.trim()
    && import.meta.env.VITE_LEGAL_PUBLISHER_EMAIL?.trim()
    && import.meta.env.VITE_LEGAL_PRIVACY_EMAIL?.trim()
    && (isProtectedNonProfessional || (
      import.meta.env.VITE_LEGAL_PUBLISHER_NAME?.trim()
      && import.meta.env.VITE_LEGAL_PUBLISHER_ADDRESS?.trim()
      && import.meta.env.VITE_LEGAL_PUBLISHER_EMAIL?.trim()
      && import.meta.env.VITE_LEGAL_PUBLISHER_PHONE?.trim()
      && import.meta.env.VITE_LEGAL_PUBLICATION_DIRECTOR?.trim()
    )),
  ),
}

export function legalDocumentFromPath(pathname: string): LegalDocumentKind | undefined {
  return (Object.entries(legalPaths) as Array<[LegalDocumentKind, string]>)
    .find(([, path]) => pathname.replace(/\/$/, '') === path)?.[0]
}
