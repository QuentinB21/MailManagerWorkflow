# Architecture du POC

## Principes

La frontière principale sépare l'orchestration de la décision métier. n8n déplace les données et appelle les systèmes ; l'API décide et garantit les invariants. Le fournisseur de messagerie ne traverse jamais directement cette frontière : ses données devront d'abord être converties en email normalisé.

```mermaid
flowchart LR
    W["Webhook n8n<br/>email fictif normalisé"] --> A["API /classification/process"]
    A --> E["Moteur de règles"]
    E --> D[("PostgreSQL<br/>configuration + historique")]
    A --> B{"Classé ?"}
    B -->|Oui| C["Branche classé"]
    B -->|Non| N["Branche non classé"]
    R["React"] -->|CRUD + simulation| A
    R -->|configuration et historique| D
```

React n'accède pas réellement à PostgreSQL : les flèches représentent les responsabilités fonctionnelles, tous les accès passent par l'API.

## Responsabilités

### React

- afficher la boîte, les labels, les règles et l'historique ;
- créer et modifier les règles, les activer ou les désactiver ;
- démarrer la connexion OAuth Gmail et afficher son état ;
- afficher l’état de la surveillance automatique et permettre une vérification immédiate de diagnostic ;
- envoyer un `NormalizedEmailRequest` au simulateur ;
- présenter la règle gagnante et ses critères.

React ne contient aucune logique de classement et aucune donnée fournisseur.

### API ASP.NET Core

- valider la cohérence boîte/label/règle ;
- normaliser les valeurs de configuration ;
- évaluer les règles actives par priorité croissante ;
- expliquer la décision ;
- garantir l'idempotence du traitement ;
- écrire un historique minimisé ;
- échanger et protéger le refresh token OAuth Gmail ;
- récupérer temporairement un message Gmail et appliquer le label externe.

Lorsqu’une règle choisit un label sans `ExternalLabelId`, l’API résout le nom auprès de Gmail ou crée le label utilisateur, conserve l’identifiant retourné puis vérifie la présence de cet identifiant dans la réponse de modification du message. Un identifiant devenu obsolète après une suppression ou un renommage direct dans Gmail est automatiquement résolu de nouveau.

Le `ClassificationEngine` ne dépend ni d'EF Core ni de n8n. Il reçoit un email et une collection de règles, ce qui le rend testable en mémoire. `EmailProcessingService` porte la lecture EF Core et la persistance idempotente.

### PostgreSQL

PostgreSQL stocke :

- `MailboxConnections` : racine fonctionnelle d'une boîte ;
- `LabelDefinitions` : labels internes et futur identifiant fournisseur ;
- `ClassificationRules` : destination, priorité, état et critères ;
- `ProcessingLogs` : identifiant externe, décision et explication.

L'index unique `(MailboxConnectionId, ExternalMessageId)` constitue la barrière d'idempotence. Le sujet, l'expéditeur et le corps ne sont pas stockés dans l'historique. Les tableaux de critères sont des colonnes PostgreSQL `text[]`.

Le client secret OAuth est stocké chiffré dans `GmailOAuthConfiguration` et le refresh token Gmail sur `MailboxConnection`. Les clés ASP.NET Core Data Protection vivent dans un volume Docker distinct afin qu’un rebuild de l’API ne rende pas ces données illisibles. Le Client ID peut être relu par l’interface, mais le secret n’est jamais renvoyé. Le corps et le sujet complet des emails Gmail ne sont jamais persistés.

### n8n

- déclenche automatiquement une vérification chaque minute et conserve un webhook de diagnostic ;
- produit le contrat normalisé ;
- appelle `/api/classification/process` ;
- choisit la branche selon `isClassified` ;
- déclenche le traitement réel Gmail sans transporter le contenu complet des messages.

Le workflow exporté transporte explicitement `mailboxConnectionId`. Il n'implémente aucune règle complexe.

Le workflow Gmail réel récupère l’unique boîte active, transporte son `mailboxConnectionId`, puis déclenche l’opération côté API sans contenu d’email. L’API recherche les messages arrivés après la connexion OAuth, filtre ceux déjà journalisés, normalise temporairement chaque nouveau message, appelle le même moteur métier testable et applique le label externe. Cette frontière empêche le corps complet de se retrouver dans les données d’exécution n8n.

L'image n8n contient un bootstrap idempotent. Elle compare l’empreinte du workflow versionné à celle importée dans le volume `n8n-data` : un JSON modifié est automatiquement réimporté et publié, tandis qu’un rebuild sans modification ne crée aucun doublon. L’option `N8N_BOOTSTRAP_FORCE_IMPORT=true` permet toujours un remplacement explicite.

## Modèle de correspondance

Une règle possède jusqu'à quatre groupes : adresses d'expéditeur, domaines, mots-clés du sujet et mots-clés du corps. Une correspondance dans une liste suffit pour que son groupe corresponde. Le mode `Any` accepte un groupe correspondant ; le mode `All` exige tous les groupes non vides. La première règle correspondante gagne après tri par `Priority` croissante, puis par date et identifiant pour un résultat déterministe.

## Évolution vers plusieurs utilisateurs

La première évolution ajoutera `UserId` à `MailboxConnection`, puis appliquera le périmètre de la boîte à chaque requête authentifiée. Les labels, règles et historiques étant déjà liés à `MailboxConnectionId`, leur schéma ne nécessite pas de refonte.

Les étapes futures envisagées sont :

1. ajouter l'identité et l'autorisation sans les mélanger au moteur de règles ;
2. permettre plusieurs connexions par utilisateur ;
3. introduire des adaptateurs `GmailEmailAdapter` puis `OutlookEmailAdapter` vers le contrat normalisé ;
4. ajouter `SummaryRequest` et `SummaryResult`, toujours rattachés à une boîte ;
5. déplacer les clés et jetons fournisseur vers un coffre de secrets en environnement partagé, jamais dans Git ni en clair dans PostgreSQL.

Cette préparation ne constitue pas une implémentation multi-utilisateur : aucun utilisateur, rôle ou tenant n'existe dans le MVP. Le flux OAuth Gmail actuel reste rattaché à l’unique `MailboxConnection` locale.
