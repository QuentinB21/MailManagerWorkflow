# Socle juridique et RGPD

Ce document décrit les mesures techniques préparées dans le dépôt. Il ne remplace pas la validation d’un professionnel du droit adaptée à l’identité de l’exploitant, à son pays d’établissement, à ses clients et à son modèle commercial.

## Fonctionnalités présentes

- pages publiques : mentions légales, politique de confidentialité, conditions d’utilisation et cookies ;
- liens accessibles depuis l’accueil, l’application et les écrans Keycloak ;
- acceptation versionnée des conditions avant le premier accès à l’application ;
- export JSON des données d’un utilisateur sans aucun secret OAuth ;
- effacement en cascade des boîtes, destinations, règles, historiques et preuve d’acceptation ;
- tentative de révocation du jeton Gmail avant l’effacement ;
- purge automatique de l’historique après 90 jours par défaut ;
- absence de corps d’email dans la base et les journaux applicatifs ;
- politique cookies sans bandeau tant qu’aucun traceur non nécessaire n’est installé.

L’acceptation porte juridiquement sur les conditions d’utilisation. La politique de confidentialité est portée à la connaissance de l’utilisateur ; elle n’utilise pas artificiellement le consentement comme base légale pour le service demandé.

## Informations à compléter avant publication

Renseigner les variables `LEGAL_*` applicables dans le fichier `.env`, puis reconstruire le frontend :

```powershell
docker compose build web
docker compose up -d web
```

Les pages affichent volontairement `[À compléter …]` en développement lorsqu’une donnée manque. Une instance publique ne doit jamais conserver ces marqueurs. Ces variables sont intégrées au JavaScript public pendant la construction : n’y placez jamais une donnée que vous souhaitez garder confidentielle.

Deux modes sont prévus :

- `LEGAL_PUBLISHER_DISCLOSURE_MODE=identified` : identité, domicile ou adresse professionnelle, téléphone et direction de la publication sont affichés ;
- `LEGAL_PUBLISHER_DISCLOSURE_MODE=protected-non-professional` : le domicile et le téléphone personnel ne sont pas intégrés au site. Le nom et un contact restent indiqués pour identifier le responsable du traitement au titre du RGPD. Ce régime n’est utilisable que pour une édition réellement non professionnelle et si l’hébergeur détient l’identité complète et à jour de l’éditeur, conformément à l’article 1-1, II de la LCEN.

Le second mode doit être abandonné avant toute exploitation professionnelle ou commerciale. Une domiciliation professionnelle permet alors de ne pas publier l’adresse personnelle.

Selon la forme de l’exploitant, vérifier notamment :

- dénomination ou nom complet, forme juridique et adresse ;
- SIREN/SIRET ou immatriculation applicable ;
- adresse électronique et téléphone ;
- directeur de la publication ;
- nom, adresse et téléphone de l’hébergeur réel du VPS ;
- adresse dédiée à l’exercice des droits RGPD ;
- le cas échéant, capital social, numéro de TVA et données propres à une activité réglementée.

## Registre simplifié des traitements

| Traitement | Données principales | Finalité | Base envisagée | Conservation |
|---|---|---|---|---|
| Compte Keycloak | identité, courriel, identifiant, empreinte de mot de passe | authentification et gestion du compte | exécution du service | durée du compte |
| Connexion OAuth | adresse de boîte, fournisseur, scopes, jeton chiffré | accéder à la boîte sur instruction de l’utilisateur | exécution du service | jusqu’à déconnexion ou effacement |
| Classement | expéditeur, sujet et corps traités en mémoire | évaluer les règles et appliquer une destination | exécution du service | corps non conservé |
| Configuration | destinations, règles et critères | personnaliser le classement | exécution du service | durée du compte ou suppression |
| Historique | identifiant message, aperçu du sujet, décision, erreur | preuve de traitement et diagnostic | exécution du service / intérêt légitime | 90 jours par défaut |
| Sécurité | événements techniques strictement nécessaires | prévention des abus et disponibilité | intérêt légitime | à fixer dans la politique d’exploitation |
| Acceptation juridique | sujet Keycloak, versions et date | prouver les conditions applicables | exécution du service / preuve | durée du compte |

Ce tableau doit être complété avec les responsables internes, destinataires, mesures de sécurité, transferts et contrats de sous-traitance réels.

## Cookies

Le code public ne charge ni publicité ni mesure d’audience. Keycloak utilise seulement des cookies de session et de sécurité indispensables à l’authentification. Dans cette configuration, aucun bandeau de consentement n’est nécessaire. Toute future intégration de statistiques, support tiers, vidéo intégrée ou publicité doit faire l’objet d’un nouvel audit avant activation.

La CNIL rappelle que les traceurs strictement nécessaires au service demandé sont exemptés de consentement, contrairement aux autres traceurs : <https://www.cnil.fr/fr/cookies-et-autres-traceurs/que-dit-la-loi>.

## Exercice des droits

L’utilisateur peut télécharger ses données et effacer ses données applicatives depuis **Boîtes > Données personnelles**. La suppression des identifiants Keycloak reste une opération distincte dans la version actuelle ; l’exploitant doit traiter cette suppression depuis Keycloak lorsqu’elle est demandée par l’adresse RGPD.

La procédure interne doit préciser :

1. comment vérifier l’identité uniquement en cas de doute raisonnable ;
2. comment retrouver les données Keycloak, PostgreSQL, sauvegardes et journaux ;
3. comment répondre dans les délais du RGPD ;
4. comment documenter la réponse et les éventuelles restrictions ;
5. comment informer la personne d’un droit de réclamation auprès de la CNIL.

Référence CNIL : <https://www.cnil.fr/fr/preparer-lexercice-des-droits-des-personnes>.

## Mesures organisationnelles restant à réaliser

- signer ou vérifier les clauses de sous-traitance avec l’hébergeur et les prestataires ;
- documenter Google et Microsoft comme destinataires/fournisseurs de services et contrôler les transferts hors EEE ;
- définir la rotation des sauvegardes et la suppression différée des données qui s’y trouvent ;
- limiter et journaliser les accès administrateurs au VPS, à PostgreSQL, Keycloak et n8n ;
- prévoir une procédure de violation de données, incluant l’évaluation de la notification CNIL sous 72 heures ;
- configurer HTTPS, les sauvegardes chiffrées, la rotation des secrets et des durées de journaux Docker ;
- configurer SMTP dans Keycloak, la vérification des courriels et la récupération sécurisée du mot de passe avant une ouverture publique ;
- vérifier si une analyse d’impact est nécessaire selon l’échelle, les clients et les futurs usages d’IA.

## Évolutions nécessitant une nouvelle revue

### Offre payante

Avant toute facturation à des particuliers, ajouter des CGV adaptées : prix TTC, paiement, renouvellement, résiliation, droit de rétractation ou exception applicable, garanties légales, médiateur de la consommation et informations précontractuelles. Les CGU présentes ne remplacent pas des CGV.

Le régime `protected-non-professional` doit également être réévalué avant l’ouverture d’une option payante. Prévoir en amont l’immatriculation appropriée, une domiciliation distincte du domicile personnel si souhaité, puis les mentions d’identification correspondantes.

### Analyse par intelligence artificielle

Avant l’envoi du moindre email à un fournisseur d’IA :

- choisir et contractualiser le fournisseur ;
- mettre à jour la politique avec les données envoyées, finalités, conservation, transferts et sous-traitants ;
- informer les personnes de façon claire avant l’activation ;
- définir une base légale, des limites de contenu et une validation humaine ;
- mettre à jour le registre et, si nécessaire, réaliser une analyse d’impact.

### Mesure d’audience ou marketing

Ne pas charger le traceur avant le choix de l’utilisateur. Le refus doit être aussi simple que l’acceptation et le retrait toujours accessible.

## Sources de référence

- transparence RGPD : <https://www.cnil.fr/fr/conformite-rgpd-information-des-personnes-et-transparence> ;
- obligations RGPD des entreprises : <https://entreprendre.service-public.fr/vosdroits/F24270> ;
- article 1-1 de la LCEN : <https://www.legifrance.gouv.fr/codes/article_lc/LEGIARTI000049568614> ;
- mentions légales et LCEN : <https://entreprendre.service-public.fr/P10025> ;
- recommandations cookies : <https://www.cnil.fr/fr/cookies-et-autres-traceurs/regles>.
