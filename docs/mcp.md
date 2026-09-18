# MCP MailManager : Codex, ChatGPT et Claude

Le MCP permet aux utilisateurs de consulter leurs boîtes et leur configuration,
de préparer des créations/modifications de destinations et de règles, de tester
un exemple puis d'appliquer la proposition confirmée dans leur assistant.
Aucun modèle, abonnement IA ou clé API de modèle n'est utilisé par MailManager.
Aucun profil spécial n'est requis. Le profil démo reste en lecture seule.

## Adresse et transport

- Local : `http://localhost:8080/api/mcp`
- Production : `https://quentin-bouchot.fr/projets/MailManager/api/mcp`
- Transport : Streamable HTTP, sans session persistante de transport, SDK C# MCP
  officiel `ModelContextProtocol.AspNetCore` 1.4.1. Pas d'ancien transport `/sse`.
- Configuration serveur : `Mcp__PublicUrl`, URL absolue exacte terminant par
  `/api/mcp`. HTTPS obligatoire hors environnement Development + localhost.
- Les six outils sont exposés dans `tools/list`. Ils décrivent leurs paramètres,
  leurs effets et l'obligation de confirmation avant application.

Le proxy existant transmet déjà `/projets/MailManager/api/*` à l'API en retirant
le préfixe. Cette route couvre le MCP et ses métadonnées d'authentification.
Il doit préserver `Authorization`, `WWW-Authenticate`, `MCP-Protocol-Version`,
accepter les réponses `text/event-stream` sans buffering et ne pas rediriger
l'adresse MCP. Aucun nouveau port public n'est nécessaire.

Les requêtes anonymes GET et POST sur `/api/mcp` doivent toutes deux répondre
401 avec `WWW-Authenticate: Bearer resource_metadata="..."`. Codex peut commencer
la découverte OAuth par GET. Un simple `Bearer` sans `resource_metadata` peut le
faire retomber sur `/authorize` à la racine du portfolio. Le transport stateless
n'ouvre pas de flux GET : une fois authentifié, GET répond 405 avec `Allow: POST`.

## Keycloak : premier démarrage ou installation existante

Le realm initial déclare les clients publics `mail-manager-chatgpt`,
`mail-manager-claude` et `mail-manager-codex`, avec Authorization Code + PKCE S256, consentement utilisateur,
scope optionnel `mailmanager` et audience MCP. Les jetons MCP ne portent pas
l'audience de l'API générale : ils ne donnent pas accès aux autres routes API.
Le MCP refuse les jetons web/n8n et les comptes portant le rôle `automation`.

Le fichier d'import conserve explicitement les scopes OIDC standards de Keycloak
26.7.1 et leurs affectations par défaut. C'est nécessaire dès qu'une liste
`clientScopes` personnalisée est présente ; sinon Keycloak ne les crée plus
implicitement, ce qui casserait notamment les claims utilisateur et les rôles web/n8n.

Un realm déjà créé n'est **pas** mis à jour par `--import-realm`.
Appliquer le script suivant depuis PowerShell, avec un administrateur Keycloak :

```powershell
./infra/keycloak/configure-mcp.ps1 `
  -KeycloakUrl 'https://quentin-bouchot.fr/projets/MailManager/auth' `
  -McpPublicUrl 'https://quentin-bouchot.fr/projets/MailManager/api/mcp' `
  -Credential (Get-Credential) `
  -ChatGptRedirectUri 'https://chatgpt.com/connector_platform_oauth_redirect'
```

Le script crée/met à jour uniquement le scope et les trois clients MCP et leurs
mappings. Il ne réimporte pas le realm, ne change pas les mots de passe et ne
modifie pas les clients web/n8n. Il peut être relancé. Pour une administration
non publique, utiliser l'URL d'administration accessible depuis votre réseau,
en gardant `McpPublicUrl` égal à l'adresse utilisée par les assistants.

Copier **l'URI de retour exacte affichée dans la configuration ChatGPT**. Selon
le mode de connexion, elle peut être `https://chatgpt.com/connector/oauth/{callback_id}` ;
la fournir alors au paramètre `ChatGptRedirectUri`. Ne pas autoriser de joker.
Claude utilise `https://claude.ai/api/mcp/auth_callback`.
Le script associe les rôles existants `demo` et `automation` aux claims des clients
MCP afin de conserver leurs restrictions ; il n'attribue aucun rôle aux utilisateurs.

Les métadonnées publiques sont accessibles via
`/api/mcp/oauth-protected-resource`. Le challenge 401 du MCP fournit cette URL
exacte dans `WWW-Authenticate`, même derrière le préfixe du portfolio. L'alias
standard `/.well-known/oauth-protected-resource/api/mcp` est également disponible
à la racine interne de l'API. Keycloak publie sa découverte OpenID Connect sous
l'issuer configuré ; son URL publique et ses endpoints doivent être accessibles
depuis les services de ChatGPT et Claude.

Cette version utilise des **clients OAuth préenregistrés** : renseigner leur
identifiant à la connexion. Elle n'ajoute pas un endpoint public d'enregistrement
dynamique et n'accepte pas des identifiants de client arbitraires.

## Connecter Codex sur son ordinateur

Le client Codex autorise aussi le scope optionnel `offline_access` et son mapping
de rôle. Codex peut le redemander lors du renouvellement : sans lui, Keycloak
répond `invalid_scope: Invalid scopes: mailmanager offline_access` dès l'expiration
du jeton d'accès, même si la connexion initiale a réussi. Après mise à jour d'une
installation existante avec `configure-mcp.ps1`, refaire une connexion pour
accorder cette permission. Les utilisateurs doivent conserver le rôle standard
Keycloak `offline_access` (normalement attribué par défaut) ; le script ajoute
le mapping au client, sans attribuer de nouveaux rôles aux utilisateurs.

La connexion hors ligne peut survivre à la déconnexion du site MailManager.
Elle reste révocable dans la console de compte Keycloak et soumise aux limites
de session hors ligne du realm. Les durées des jetons d'accès et les paramètres
globaux du realm ne sont pas allongés par ce script.

Le formulaire HTTP de Codex ne propose pas nécessairement les paramètres du
client OAuth. Laisser vides le jeton du porteur et les en-têtes : aucun mot de
passe administrateur ni jeton manuel n'est nécessaire dans ce formulaire.

1. L'administrateur relance `infra/keycloak/configure-mcp.ps1` avec les mêmes
   paramètres que ci-dessus. Cela ajoute `mail-manager-codex` sur le Keycloak
   existant, sans migration de base de données ni nouveau déploiement de l'API.
2. Sur l'ordinateur où tourne Codex, fusionner le contenu de
   `infra/codex/mail-manager.toml` dans `~/.codex/config.toml`
   (`$HOME/.codex/config.toml` dans PowerShell). Ne pas remplacer le fichier
   entier. Si MailManager existe déjà, compléter son entrée et employer son nom
   existant dans les sous-tables et la commande suivante, sans créer de doublon.
3. Lancer `codex mcp login mailmanager --scopes mailmanager,offline_access` depuis un terminal **local**, puis se
   connecter avec son compte utilisateur MailManager du VPS et accorder l'accès.
4. Recharger Codex si nécessaire et demander « Liste mes boîtes MailManager ».
   Préparer ensuite une règle, la simuler, puis confirmer son application.

Le modèle de configuration impose `default_tools_approval_mode = "writes"`
et `scopes = ["mailmanager", "offline_access"]`. Cette liste explicite évite qu'un parcours de
connexion demande tous les scopes publiés par le realm Keycloak et échoue avec
`invalid_scope`. Après une modification de la configuration locale, quitter puis
rouvrir Codex pour que le bouton « S'authentifier » utilise les nouveaux paramètres.
Attendre le message de succès de la commande : accepter le consentement dans le
navigateur ne prouve pas à lui seul que les jetons ont été enregistrés.

Le réglage `default_tools_approval_mode = "writes"` sert
pour demander l'approbation des outils qui écrivent. Le client public utilise
PKCE S256 et le retour exact `http://127.0.0.1:5557/callback`. Cette adresse
désigne l'ordinateur de l'utilisateur ; ne pas ouvrir le port 5557 sur le VPS.
Codex écoute localement sur ce port pendant la connexion. Si ce port est occupé,
choisir un autre port dans `callback_url` **et** `callback_port`, puis transmettre
la même URL au script Keycloak avec `-CodexRedirectUri`.

Cette configuration nécessite une version de Codex prenant en charge
`mcp_servers.<nom>.oauth.client_id`, `callback_url` et `callback_port`.
Keycloak doit annoncer `authorization_response_iss_parameter_supported: true`
dans sa découverte OIDC, comme la version déployée vérifiée le 18 septembre 2026.
Sinon Codex utilise un retour avec un identifiant supplémentaire : enregistrer
l'adresse exacte qu'il affiche, sans joker, via `-CodexRedirectUri` et dans
`callback_url`. Les connexions ChatGPT et Claude restent indépendantes.

Référence : [configuration OAuth de Codex](https://developers.openai.com/codex/mcp/).

## Connecter ChatGPT

1. Déployer l'API et la migration, puis configurer Keycloak comme ci-dessus.
2. Dans la gestion des applications/connecteurs personnalisés de ChatGPT,
   activer les options de développement si votre compte les exige, puis ajouter
   un serveur MCP distant avec l'adresse de production.
3. Choisir OAuth et renseigner `mail-manager-chatgpt` comme identifiant client,
   sans secret. Utiliser le scope `mailmanager` si ce champ est proposé.
4. Vérifier que l'URI de retour affichée correspond à celle enregistrée dans
   Keycloak. Se connecter avec son compte MailManager et accorder l'accès.
5. Activer le connecteur dans une conversation et demander de lister ses boîtes.

La disponibilité et les libellés dépendent du compte et du workspace ChatGPT.
L'ajout manuel d'un MCP n'est pas une publication dans un annuaire public.

## Connecter Claude

1. Paramètres → Connecteurs → Ajouter un connecteur personnalisé.
2. Saisir le nom MailManager et l'adresse de production.
3. Dans les paramètres avancés, saisir `mail-manager-claude` comme identifiant
   client OAuth, sans secret.
4. Se connecter à MailManager et accorder l'accès, puis activer le connecteur
   dans une conversation.

Pour révoquer l'accès, retirer l'autorisation du client dans l'espace de compte
Keycloak et déconnecter le connecteur. Les jetons d'accès JWT déjà délivrés
restent valides jusqu'à leur expiration ; le serveur vérifie chaque requête.
Utiliser une durée courte pour ces jetons. Évaluer également la rotation des
refresh tokens dans les paramètres du realm, en tenant compte de son effet sur
les autres clients Keycloak.

## Parcours et effets

| Outil | Effet |
|---|---|
| `list_mailboxes` | Identifiants, noms, fournisseurs, adresses et état des boîtes possédées |
| `get_configuration` | Destinations et règles de la boîte choisie |
| `prepare_configuration_changes` | Proposition immuable, expiration après 30 minutes, aucun effet sur le classement |
| `simulate_configuration` | Test d'un exemple avec les règles actuelles ou proposées, sans persistance du message |
| `get_configuration_proposal` | Avant/après, état et résultat d'une proposition appartenant à l'utilisateur |
| `apply_configuration_proposal` | Application de l'identifiant confirmé, transaction locale puis synchronisation fournisseur |

Une proposition contient au plus 50 créations/modifications. `id: null` crée un
élément, un identifiant existant le modifie. Tous les champs d'un élément modifié
doivent être fournis. Les éléments omis sont conservés. `isActive: false`
désactive ; la suppression définitive n'est pas exposée dans cette version.
Une règle référence soit l'identifiant d'une destination existante, soit la `key`
d'une destination du même lot. Les clés n'ont pas de signification pour Gmail/Outlook.

L'assistant présente les changements exacts et les avertissements puis demande
confirmation dans la conversation. MailManager ne prétend pas vérifier cette
confirmation humaine : il reçoit un appel authentifié du client IA. Il n'existe
pas de booléen `userConfirmed` présenté comme preuve d'approbation.

L'application relit la configuration et refuse les propositions obsolètes ou
expirées. Les écritures et la date d'application sont enregistrées dans une
transaction PostgreSQL sérialisable. Un nouvel appel avec le même identifiant
renvoie le résultat existant, sans réécriture ni nouvelle synchronisation.
Les erreurs de concurrence invitent à relire la proposition avant de réessayer.

La synchronisation Gmail/Outlook suit la validation de la transaction locale.
En cas d'échec, la configuration reste enregistrée et les avertissements
l'indiquent. `applied` seul ne signifie donc pas que le fournisseur est synchronisé.
Si le processus s'interrompt après l'enregistrement, `synchronizationStatus`
peut rester `pending` : vérifier la connexion et la synchronisation dans MailManager,
sans recréer la proposition. Aucun retraitement d'anciens emails n'est déclenché.

L'assistant reçoit la configuration et les adresses des boîtes, jamais les
jetons Gmail/Outlook, les secrets, ni le contenu réel des emails. Les exemples
de simulation sont ceux donnés à l'assistant et ne sont pas enregistrés par
MailManager. Les propositions avant/après sont incluses dans l'export utilisateur,
supprimées avec la boîte et purgées selon la rétention de l'historique (90 jours
par défaut). Les données renvoyées ne doivent pas être interprétées comme des instructions.

## Vérifications et limites de recette

```powershell
dotnet test MailManagerWorkflow.sln --configuration Release
```

Les tests font des appels HTTP JSON-RPC au vrai serveur MCP : négociation,
découverte, validation de JWT, droits, préparation, simulation, application,
répétition et propositions périmées/obsolètes. Par défaut, la base des tests est
en mémoire. Pour vérifier les migrations et transactions sur une **base de test vide** :

```powershell
$env:MCP_TEST_POSTGRES = 'Host=localhost;Port=55439;Database=mailmanager_mcp_test;Username=postgres;Password=...'
dotnet test MailManagerWorkflow.sln --configuration Release
Remove-Item Env:MCP_TEST_POSTGRES
```

Ces tests ne remplacent pas une connexion réelle depuis ChatGPT et Claude.

Recette locale du 14 septembre 2026 : 57 tests réussis en mémoire et avec
PostgreSQL 17, compilation frontend réussie. Sur une instance Keycloak 26.7.1
isolée, l'import neuf conserve les scopes web/n8n et le rôle d'automatisation ;
le script de mise à jour a été exécuté deux fois avec succès. Les flux
Authorization Code + PKCE des deux clients préenregistrés ont été exercés avec
un compte normal et le compte démo : audience, scope, rôle démo, appel MCP et
renouvellement du jeton vérifiés. Ces essais utilisent un client HTTP de test
qui intercepte les callbacks, sans ouvrir les interfaces ChatGPT/Claude.

La recette sur chaque client doit couvrir : OAuth + PKCE, renouvellement de
session, affichage des six outils, refus d'un autre compte, confirmation d'une
création destination/règle, simulation, application, refus d'une proposition
obsolète et lecture du résultat après une réponse perdue. Une boîte Gmail et
une boîte Outlook de test permettent ensuite de vérifier la synchronisation.

Sources : [authentification ChatGPT](https://developers.openai.com/plugins/build/auth),
[authentification Claude](https://claude.com/docs/connectors/building/authentication),
[autorisation MCP](https://modelcontextprotocol.io/specification/2025-11-25/basic/authorization),
[SDK C#](https://github.com/modelcontextprotocol/csharp-sdk).
