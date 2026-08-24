# Authentification Keycloak

## Vue d’ensemble

Keycloak est l’unique fournisseur d’identité de Mail Manager. Le frontend utilise le flux OpenID Connect **Authorization Code avec PKCE S256** ; aucun secret client n’est embarqué dans React. L’API valide la signature, l’émetteur, l’audience et l’expiration de chaque jeton.

Le realm `mail-manager` contient trois usages distincts :

| Identité | Accès |
|---|---|
| utilisateur inscrit | ses propres boîtes, règles, destinations et historiques |
| `demo` | boîte factice en lecture seule et endpoint de simulation uniquement |
| client `mail-manager-n8n` | traitement automatisé de toutes les boîtes connectées, sans interface utilisateur |

L’isolation repose sur `MailboxConnection.OwnerSubject`, qui contient le claim OpenID Connect `sub`. Les labels, règles et historiques héritent de cette frontière par leur `MailboxConnectionId`.

## Configuration locale

Copiez `.env.example` vers `.env`, puis remplacez au minimum :

```dotenv
KEYCLOAK_ADMIN_PASSWORD=<mot-de-passe-administrateur-long-et-aléatoire>
KEYCLOAK_LOCAL_USER_PASSWORD=<mot-de-passe-du-compte-owner>
N8N_API_CLIENT_SECRET=<secret-de-service-long-et-aléatoire>
```

Le mot de passe du profil public `demo` est volontairement public : son compte ne peut ni modifier la configuration, ni connecter une boîte, ni appeler les traitements réels.

Au premier `docker compose up --build -d`, le realm versionné est importé depuis `infra/keycloak/import/mail-manager-realm.json`. Keycloak ignore un realm déjà présent lors des démarrages suivants afin de préserver les comptes et sessions. Une évolution du JSON doit donc être appliquée explicitement dans la console d’administration ou via une procédure de migration Keycloak ; le fichier d’import n’est pas un mécanisme de mise à jour destructive.

## Accès locaux

- application : [http://localhost:5173](http://localhost:5173) ;
- connexion Keycloak : [http://localhost:8081/realms/mail-manager/account](http://localhost:8081/realms/mail-manager/account) ;
- administration Keycloak : [http://localhost:8081/admin/](http://localhost:8081/admin/).

Le compte `owner` est créé pour reprendre les boîtes existantes lors de la migration depuis la version sans authentification. Les nouvelles inscriptions commencent avec un espace vide et peuvent ensuite ajouter leurs propres boîtes Gmail ou Outlook.

## Thème

Le thème `infra/keycloak/themes/mail-manager` étend `keycloak.v2`. Il applique la palette, la typographie et les composants visuels de l’application aux pages de connexion, d’inscription et d’erreur. L’image Keycloak copie au build les fichiers `apps/web/public/logo.svg` et `apps/web/public/favicon.ico`, qui restent ainsi les sources uniques de l’identité visuelle, y compris dans l’onglet du navigateur. Le script du thème transforme progressivement l’inscription en deux étapes (`Profil`, puis `Sécurité`) sans remplacer le formulaire ni la validation de Keycloak. Le changement d’étape utilise une transition directionnelle courte, automatiquement désactivée lorsque le navigateur demande une réduction des animations. Il reconnaît également la demande explicite `login_hint=demo` émise par le bouton d’accueil et ouvre automatiquement la session du compte de démonstration partagé ; ce compte reste limité par le rôle `demo` et son identifiant n’est pas un secret utilisateur.

## Préparation du VPS

La configuration locale contient volontairement des URL `http://localhost`. Avant le déploiement public :

1. exposer Keycloak et l’application uniquement en HTTPS derrière le reverse proxy ;
2. remplacer `KC_HOSTNAME`, les URI de redirection et les origines Web du client `mail-manager-web` par les domaines publics exacts ;
3. conserver le backchannel Keycloak sur le réseau Docker privé ;
4. remplacer tous les secrets de développement et les placer dans le gestionnaire de secrets du serveur ;
5. activer et configurer l’envoi d’emails Keycloak avant d’activer la vérification d’adresse ou la réinitialisation de mot de passe ;
6. limiter l’accès public à la console d’administration Keycloak.

La configuration de production ne doit pas utiliser `start-dev`. Le conteneur devra être construit avec `kc.sh build`, démarré avec `start`, et recevoir ses certificats ou les en-têtes proxy selon l’architecture du VPS.
