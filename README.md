# Mail Manager Workflow

POC mono-utilisateur de classement automatisé d'emails. Le flux démontrable reçoit un email normalisé fictif dans n8n, délègue la décision à une API ASP.NET Core, puis conserve un historique minimal dans PostgreSQL. L'interface React permet de gérer les règles et de simuler leur résultat sans toucher à une boîte Gmail réelle.

## Ce que couvre le MVP

- labels et règles rattachés à une `MailboxConnection` ;
- interface React organisée autour du classement, de l’activité et des paramètres Gmail ;
- création, modification, activation, désactivation et suppression des labels ;
- création, modification, activation, désactivation et suppression des règles ;
- critères par adresse, domaine, sujet ou corps, avec modes `Any` et `All` ;
- première règle correspondante selon la priorité (la valeur numérique la plus petite gagne) ;
- simulation explicable sans écriture dans l'historique ;
- traitement idempotent par couple `(MailboxConnectionId, ExternalMessageId)` ;
- workflow n8n importable avec branches « classé » et « non classé » ;
- connexion OAuth d’une boîte Gmail depuis React et classement automatique des nouveaux emails ;
- création et application automatiques du label réel dans Gmail ;
- historique sans stockage du sujet ni du corps complet.

## Structure

```text
apps/api/                    API .NET 8, EF Core et migrations PostgreSQL
apps/web/                    React 19, TypeScript et Vite
tests/MailManager.Api.Tests/ Tests métier xUnit
infra/n8n/workflows/         Exports JSON n8n versionnés
docs/                        Documentation d'architecture
compose.yml                  Environnement local complet
```

## Prérequis

La voie recommandée nécessite uniquement Docker Desktop avec Docker Compose. Pour travailler hors conteneurs, il faut aussi le SDK .NET 8, Node.js 24 et pnpm 11.

## Démarrage avec Docker Compose

1. Créez la configuration locale, puis remplacez les deux valeurs sensibles par des valeurs locales :

   ```powershell
   Copy-Item .env.example .env
   notepad .env
   ```

2. Construisez et démarrez les quatre services :

   ```powershell
   docker compose up --build -d
   docker compose ps
   ```

3. Ouvrez :

   - React : [http://localhost:5173](http://localhost:5173)
   - Swagger : [http://localhost:8080/swagger](http://localhost:8080/swagger)
   - n8n : [http://localhost:5678](http://localhost:5678)
   - santé API : [http://localhost:8080/health](http://localhost:8080/health)

Au premier démarrage, l'API applique automatiquement les migrations dans le conteneur et crée une boîte de démonstration ainsi qu'un label `Projet Démo`. L'image n8n importe et publie automatiquement le workflow JSON versionné s'il n'existe pas encore. PostgreSQL est exposé sur le port hôte `5433` pour éviter les installations locales courantes sur `5432`.

Pour connecter une vraie boîte, l’exploitant configure une seule fois le client OAuth Google dans le fichier `.env`, puis l’utilisateur ouvre **Paramètres** et clique sur **Connecter mon compte Gmail**. Voir [docs/gmail-setup.md](docs/gmail-setup.md).

Pour arrêter l'environnement sans supprimer les données :

```powershell
docker compose down
```

`docker compose down -v` supprimerait aussi les volumes PostgreSQL et n8n ; ne l'utilisez que si vous souhaitez réellement réinitialiser toutes les données locales.

### Persistance et synchronisation n8n

Le compte n8n, les workflows et les exécutions sont conservés dans le volume nommé `n8n-data`. Un `docker compose up --build`, un redémarrage ou un `docker compose down` normal ne les efface pas.

Au démarrage, `infra/n8n/bootstrap.sh` utilise un marqueur persistant contenant l’empreinte du JSON de chaque workflow :

- marqueur absent ou empreinte modifiée : le JSON embarqué dans l'image est importé et publié ;
- empreinte inchangée : l'import est ignoré pour ne pas écraser les modifications faites dans l'éditeur ;
- aucun doublon n'est créé lors des rebuilds.

Si le JSON Git doit volontairement remplacer la version présente dans n8n, utilisez une synchronisation forcée après avoir exporté toute modification utile faite dans l'éditeur :

```powershell
$env:N8N_BOOTSTRAP_FORCE_IMPORT='true'
docker compose up --build --force-recreate -d n8n
Remove-Item Env:N8N_BOOTSTRAP_FORCE_IMPORT
```

Cette commande est volontairement explicite car elle remplace la définition du workflow portant le même identifiant.

## Migrations EF Core

Compose les applique automatiquement avec `Database__ApplyMigrations=true`. Pour les appliquer depuis l'hôte :

```powershell
dotnet tool restore
$env:ConnectionStrings__Postgres='Host=localhost;Port=5433;Database=mailmanager;Username=mailmanager;Password=<mot-de-passe-du-fichier-.env>'
dotnet tool run dotnet-ef database update --project apps/api/MailManager.Api.csproj --startup-project apps/api/MailManager.Api.csproj
```

Pour créer une migration après un changement de modèle :

```powershell
dotnet tool run dotnet-ef migrations add NomDeLaMigration --project apps/api/MailManager.Api.csproj --startup-project apps/api/MailManager.Api.csproj --output-dir Data/Migrations
```

## Tester le workflow n8n

1. Dans n8n, terminez si nécessaire la configuration locale du propriétaire n8n. Le workflow `POC - Classement d'un email fictif` est déjà importé et publié par le bootstrap.
2. Dans React, créez ou activez une règle, par exemple domaine `client.fr` → `Projet Démo`.
3. Dans **Classement > Tester les règles**, renseignez un expéditeur comme `alice@client.fr`.
4. Cliquez sur **Tester le workflow n8n**.

Le résultat affiche la branche n8n (`classified` ou `unclassified`), le label, la règle et les critères correspondants. La décision est enregistrée et la section **Historique n8n / API** est actualisée automatiquement. Le bouton **Simuler via l'API** reste disponible pour tester le moteur sans écrire dans l'historique.

La section **Classement** permet de gérer les destinations et les règles sans passer par Swagger. La suppression d’une destination déjà référencée par une règle est bloquée : il faut d’abord modifier ou supprimer la règle concernée afin de préserver l’intégrité de la configuration.

Pour créer un traitement distinct, changez l'identifiant externe. Réutiliser le même identifiant teste l'idempotence et affiche `wasAlreadyProcessed: true` sans créer de doublon.

Le même test peut aussi être lancé au terminal si nécessaire :

   ```powershell
   $body = @{
     mailboxConnectionId = '11111111-1111-1111-1111-111111111111'
     externalMessageId = 'gmail-demo-001'
     sender = 'contact@exemple.fr'
     subject = 'Projet Alpha - point hebdomadaire'
     body = 'Bonjour, voici les prochaines étapes.'
   } | ConvertTo-Json

   Invoke-RestMethod -Method Post `
     -Uri 'http://localhost:5678/webhook/mail-manager/email' `
     -ContentType 'application/json; charset=utf-8' `
     -Body ([Text.Encoding]::UTF8.GetBytes($body))
   ```

Le workflow utilise le nom de service Compose `http://api:8080`. Si n8n est lancé hors Compose, remplacez cette URL dans le nœud HTTP par une URL joignable depuis son environnement.

## Exécuter les compilations et tests

Backend et tests :

```powershell
dotnet restore
dotnet build MailManagerWorkflow.sln
dotnet test MailManagerWorkflow.sln --configuration Release
```

Frontend :

```powershell
Set-Location apps/web
pnpm install --frozen-lockfile
pnpm build
```

## Endpoints principaux

| Méthode | Route | Rôle |
|---|---|---|
| `GET` | `/api/mailboxes` | Liste des boîtes configurées |
| `GET` | `/api/gmail/oauth/authorize` | Démarrage de la connexion OAuth Gmail |
| `GET` | `/api/gmail/oauth/callback` | Retour OAuth traité côté serveur |
| `GET` | `/api/gmail/configuration` | État de la configuration OAuth sans exposer le secret |
| `GET` | `/api/gmail/mailboxes/{id}/test` | Vérification de la connexion Gmail |
| `GET` | `/api/mailboxes/active` | Cible mono-boîte utilisée par l’automatisation n8n |
| `POST` | `/api/gmail/mailboxes/{id}/process-unread` | Traitement idempotent des nouveaux emails Gmail |
| `POST` | `/api/gmail/mailboxes/{id}/disconnect` | Révocation et suppression du jeton local |
| `GET/POST/PUT/DELETE` | `/api/labels` | CRUD des labels |
| `GET/POST/PUT/DELETE` | `/api/rules` | CRUD des règles |
| `POST` | `/api/classification/simulate` | Décision sans persistance |
| `POST` | `/api/classification/process` | Décision idempotente et historisée |
| `GET` | `/api/processing-logs` | Historique filtré par boîte |

Le fichier `apps/api/MailManager.Api.http` contient aussi des requêtes manuelles prêtes à adapter.

## Règles de correspondance

Les valeurs sont nettoyées, les espaces multiples sont réduits et les comparaisons sont insensibles à la casse. Dans un même groupe (par exemple plusieurs domaines), une valeur correspondante suffit. `Any` exige qu'au moins un groupe configuré corresponde ; `All` exige que chaque groupe configuré corresponde. Les domaines incluent leurs sous-domaines.

## Limites actuelles

- connexion Gmail limitée au développement local ; détection automatique par interrogation chaque minute plutôt que Gmail Push/Pub/Sub ;
- un seul `MailboxConnection` de démonstration, sans utilisateur ni tenant ;
- aucune authentification applicative ; n8n conserve sa propre configuration locale ;
- aucune génération de résumé ;
- aucun mécanisme de retry/dead-letter autour des appels fournisseur ;
- l'interface ne gère pas encore la création des connexions de boîte ;
- le POC journalise la décision, mais pas encore les métriques de temps permettant de mesurer le gain métier.

Voir [docs/architecture.md](docs/architecture.md) pour les responsabilités et les points d'évolution.
