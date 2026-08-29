import type { ReactNode } from 'react'
import { legalConfiguration, legalPaths, type LegalDocumentKind } from '../legal'
import { LegalLinks } from './LegalLinks'

const updatedAt = '25 août 2026'

function Section({ title, children }: { title: string; children: ReactNode }) {
  return <section className="legal-section"><h2>{title}</h2>{children}</section>
}

function LegalNotice() {
  const legal = legalConfiguration
  return <>
    <Section title="Éditeur du service">
      {legal.isProtectedNonProfessional
        ? <><dl className="legal-details"><div><dt>Nom</dt><dd>{legal.publisherName}</dd></div><div><dt>Statut</dt><dd>{legal.publisherForm}</dd></div><div><dt>Contact</dt><dd>{legal.publisherEmail}</dd></div></dl><p>En application de l’article 1-1, II de la loi n° 2004-575 du 21 juin 2004, l’éditeur non professionnel a choisi de ne pas publier son domicile. Son identité complète et ses coordonnées ont été communiquées à l’hébergeur.</p></>
        : <dl className="legal-details">
          <div><dt>Nom</dt><dd>{legal.publisherName}</dd></div>
          <div><dt>Statut</dt><dd>{legal.publisherForm}</dd></div>
          <div><dt>Adresse</dt><dd>{legal.publisherAddress}</dd></div>
          <div><dt>Immatriculation</dt><dd>{legal.registrationNumber}</dd></div>
          <div><dt>Contact</dt><dd>{legal.publisherEmail} · {legal.publisherPhone}</dd></div>
        </dl>}
    </Section>
    {!legal.isProtectedNonProfessional && <Section title="Direction de la publication"><p>{legal.publicationDirector}</p></Section>}
    <Section title="Hébergement">
      <p>{legal.hostName}<br />{legal.hostAddress}<br />{legal.hostPhone}<br />Localisation du centre de données : {legal.hostingLocation}</p>
    </Section>
    <Section title="Propriété intellectuelle">
      <p>La structure, l’interface, les textes, le logo et les éléments propres à Mail Manager sont protégés par le droit de la propriété intellectuelle. Les marques Gmail, Google, Outlook et Microsoft appartiennent à leurs titulaires respectifs. Leur mention décrit uniquement les services compatibles et n’implique aucune affiliation.</p>
    </Section>
    <Section title="Contact et signalement"><p>Pour toute question relative au service ou pour signaler un contenu illicite, contactez {legal.publisherEmail}.</p></Section>
  </>
}

function PrivacyPolicy() {
  const legal = legalConfiguration
  return <>
    <Section title="Responsable du traitement">
      {legal.isProtectedNonProfessional
        ? <p>{legal.publisherName}, éditeur non professionnel de Mail Manager et responsable du traitement. Le domicile n’est pas publié selon le régime prévu à l’article 1-1, II de la LCEN. Contact relatif aux données personnelles : {legal.privacyEmail}.</p>
        : <p>{legal.publisherName}, {legal.publisherAddress}. Contact relatif aux données personnelles : {legal.privacyEmail}.</p>}
    </Section>
    <Section title="Données traitées">
      <ul>
        <li>identité et coordonnées du compte transmises par Keycloak : identifiant, nom, prénom et courriel ;</li>
        <li>boîtes connectées : fournisseur, adresse, autorisations OAuth et jeton de renouvellement chiffré ;</li>
        <li>configuration : destinations, couleurs, règles et critères définis par l’utilisateur ;</li>
        <li>historique : identifiant fournisseur du message, aperçu limité du sujet, décision de classement et éventuelle erreur ;</li>
        <li>données techniques de sécurité nécessaires au fonctionnement et à la prévention des abus.</li>
      </ul>
      <p>Le corps des emails est lu transitoirement afin d’évaluer les règles, puis supprimé de la mémoire de traitement. Il n’est ni conservé dans la base de données ni écrit dans les journaux applicatifs. Les mots de passe Gmail et Microsoft ne sont jamais transmis à Mail Manager.</p>
    </Section>
    <Section title="Finalités et bases juridiques">
      <ul>
        <li><strong>Exécution du service demandé :</strong> création du compte, connexion des boîtes, classement et présentation de l’historique.</li>
        <li><strong>Intérêt légitime :</strong> sécurisation, diagnostic des erreurs, prévention des abus et amélioration technique limitée du service.</li>
        <li><strong>Obligations légales :</strong> réponse aux demandes d’exercice de droits et aux demandes légalement fondées des autorités.</li>
      </ul>
      <p>Aucune prospection commerciale, publicité ciblée ou décision produisant un effet juridique n’est réalisée. La génération de résumés par intelligence artificielle n’est pas active dans la version actuelle ; cette politique devra être mise à jour et les utilisateurs informés avant son activation.</p>
    </Section>
    <Section title="Destinataires et sous-traitants">
      <p>Les données sont accessibles à l’éditeur et aux prestataires strictement nécessaires à l’exploitation. Keycloak, PostgreSQL et n8n sont exploités dans l’infrastructure Mail Manager. Google et Microsoft reçoivent les requêtes nécessaires à l’autorisation OAuth et au classement dans leur propre service, conformément à leurs conditions et politiques.</p>
    </Section>
    <Section title="Durées de conservation">
      <ul>
        <li>compte, boîtes, destinations et règles : jusqu’à leur suppression ou à la suppression des données du compte ;</li>
        <li>jetons OAuth : jusqu’à la déconnexion de la boîte, la révocation fournisseur ou la suppression des données ;</li>
        <li>historique des traitements : 90 jours par défaut, puis suppression automatique ;</li>
        <li>preuve d’acceptation des conditions : pendant la durée du compte, puis suppression avec les données applicatives.</li>
      </ul>
    </Section>
    <Section title="Vos droits">
      <p>Vous pouvez demander l’accès, la rectification, l’effacement, la limitation, l’opposition lorsque celle-ci s’applique, ainsi que la portabilité de vos données. L’application permet de télécharger un export JSON et de supprimer les données Mail Manager depuis la page Boîtes.</p>
      <p>Pour toute demande : {legal.privacyEmail}. Une preuve d’identité peut être demandée uniquement en cas de doute raisonnable. Vous pouvez également adresser une réclamation à la <a href="https://www.cnil.fr/fr/plaintes" target="_blank" rel="noreferrer">CNIL</a>.</p>
    </Section>
    <Section title="Sécurité et transferts">
      <p>Les accès sont isolés par utilisateur. Les jetons OAuth sont chiffrés au repos et les secrets ne sont pas inclus dans les exports. L’infrastructure principale est hébergée par {legal.hostName}, dans un centre de données situé en {legal.hostingLocation}. Lors d’un déploiement public, toutes les communications doivent utiliser HTTPS. Google et Microsoft peuvent traiter certaines données hors de l’Espace économique européen selon les garanties décrites dans leurs propres documents contractuels.</p>
    </Section>
    <Section title="Personnes présentes dans les emails">
      <p>Les expéditeurs et destinataires dont les coordonnées ou propos figurent dans une boîte connectée peuvent être concernés indirectement. Mail Manager limite ce traitement à l’exécution des règles choisies par le titulaire de la boîte et ne constitue pas de répertoire de contacts.</p>
    </Section>
  </>
}

function Terms() {
  const legal = legalConfiguration
  return <>
    <Section title="Objet"><p>Les présentes conditions encadrent l’accès à Mail Manager, service de connexion de boîtes Gmail ou Outlook, de configuration de règles et de classement automatique des emails.</p></Section>
    <Section title="Gratuité du service actuel"><p>La version actuelle de Mail Manager est proposée gratuitement et sans publicité. Aucune fonctionnalité payante ni génération de résumé par intelligence artificielle n’est actuellement disponible. Si une option payante est proposée ultérieurement, ses prix, conditions de souscription, modalités de résiliation et règles relatives au traitement par intelligence artificielle seront présentés avant tout achat et feront l’objet de documents contractuels mis à jour.</p></Section>
    <Section title="Compte et accès">
      <p>L’utilisateur fournit des informations exactes, protège ses identifiants et avertit l’éditeur en cas d’accès non autorisé. Il ne peut connecter qu’une boîte qu’il est autorisé à administrer. Le profil de démonstration est partagé, limité à des données fictives et ne doit recevoir aucune donnée personnelle ou confidentielle.</p>
    </Section>
    <Section title="Autorisations de messagerie"><p>La connexion repose sur le consentement OAuth de Google ou Microsoft. L’utilisateur peut retirer cet accès depuis Mail Manager ou depuis les paramètres du fournisseur. Il demeure responsable des règles créées et peut à tout moment les modifier ou les désactiver.</p></Section>
    <Section title="Usage acceptable"><p>Il est interdit de contourner les mesures de sécurité, d’accéder aux données d’un tiers, de perturber le service, d’automatiser des volumes abusifs ou d’utiliser le service en violation de la loi et des droits de tiers.</p></Section>
    <Section title="Disponibilité et version d’évaluation"><p>Le service est actuellement fourni en version d’évaluation. Des interruptions de maintenance ou des erreurs peuvent survenir. L’utilisateur doit conserver l’accès à sa messagerie et vérifier les classements importants ; Mail Manager ne remplace pas les fonctions de sauvegarde du fournisseur.</p></Section>
    <Section title="Responsabilité"><p>L’éditeur met en œuvre des moyens raisonnables pour assurer le service, sans garantir l’absence totale d’erreur de classement. Dans les limites autorisées par la loi, il ne répond pas des conséquences d’une règle mal configurée, d’une indisponibilité fournisseur ou d’un usage non conforme.</p></Section>
    <Section title="Données et suppression"><p>Le traitement des données est décrit dans la <a href={legalPaths.privacy}>politique de confidentialité</a>. L’utilisateur peut exporter puis supprimer ses données applicatives. La suppression des données est irréversible.</p></Section>
    <Section title="Évolution des conditions"><p>Une modification substantielle sera présentée à l’utilisateur avant la poursuite de l’utilisation. L’acceptation est enregistrée avec la version du document et sa date.</p></Section>
    <Section title="Droit applicable et contact"><p>Les présentes conditions sont soumises au droit français, sous réserve des règles impératives protégeant les consommateurs. Toute difficulté peut être signalée à {legal.publisherEmail} afin de rechercher une solution amiable.</p></Section>
  </>
}

function Cookies() {
  return <>
    <Section title="Principe"><p>Mail Manager n’utilise actuellement aucun cookie publicitaire, outil de mesure d’audience ou traceur destiné au profilage. Aucun bandeau de consentement n’est donc affiché.</p></Section>
    <Section title="Cookies strictement nécessaires"><p>Keycloak peut déposer des cookies de session et de sécurité tels que <code>AUTH_SESSION_ID</code>, <code>KC_RESTART</code>, <code>KEYCLOAK_IDENTITY</code> ou <code>KEYCLOAK_SESSION</code>. Ils servent exclusivement à ouvrir et protéger la session, mémoriser temporairement le parcours d’authentification et permettre la déconnexion.</p></Section>
    <Section title="Durée et contrôle"><p>Les cookies temporaires expirent à la fin du parcours ou de la session ; les durées des cookies de connexion suivent la configuration de sécurité Keycloak. Les bloquer depuis le navigateur peut empêcher la connexion. Leur usage est exempté de consentement car il est indispensable au service expressément demandé.</p></Section>
    <Section title="Évolution"><p>Si un outil de mesure, de personnalisation ou un autre traceur non nécessaire est ajouté, il restera désactivé jusqu’au choix explicite de l’utilisateur et cette page sera mise à jour.</p></Section>
  </>
}

const titles: Record<LegalDocumentKind, { overline: string; title: string }> = {
  legalNotice: { overline: 'Éditeur et hébergement', title: 'Mentions légales' },
  privacy: { overline: 'Protection des données', title: 'Politique de confidentialité' },
  terms: { overline: 'Règles du service', title: "Conditions générales d’utilisation" },
  cookies: { overline: 'Traceurs techniques', title: 'Politique relative aux cookies' },
}

export function LegalPage({ kind }: { kind: LegalDocumentKind }) {
  const heading = titles[kind]
  return <main className="legal-page">
    <header className="legal-topbar"><a className="brand" href="/" aria-label="Revenir à Mail Manager"><img className="brand-mark" src="/logo.svg" alt="" /><div><strong>Mail Manager</strong><small>Classement automatique</small></div></a><a className="legal-back" href="/">← Revenir à l’application</a></header>
    <article className="legal-document">
      <header className="legal-heading"><p className="overline">{heading.overline}</p><h1>{heading.title}</h1><p>Version du {updatedAt}</p></header>
      {!legalConfiguration.isComplete && <aside className="legal-warning" role="note"><strong>Version de développement</strong><p>Les coordonnées légales de l’exploitant ne sont pas encore configurées. Les champs concernés sont signalés et devront obligatoirement être renseignés avant la publication.</p></aside>}
      {kind === 'legalNotice' && <LegalNotice />}
      {kind === 'privacy' && <PrivacyPolicy />}
      {kind === 'terms' && <Terms />}
      {kind === 'cookies' && <Cookies />}
    </article>
    <LegalLinks className="legal-footer-links" />
  </main>
}
